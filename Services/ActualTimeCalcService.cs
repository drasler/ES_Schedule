using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using Dapper;
using Oracle.ManagedDataAccess.Client;
using ES_Schedule.Common;
using ES_Schedule.Models;

namespace ES_Schedule.Services
{
    /// <summary>
    /// 工時計算服務類別
    /// </summary>
    public class ActualTimeCalcService
    {
        private readonly string _jhdbConnectionString;
        private readonly string _amesConnectionString;
        private readonly string _schemaJhdb;
        private readonly string _schemaJhdbSys;
        private readonly string _outputPath;

        public ActualTimeCalcService()
        {
            // 讀取連線字串
            _jhdbConnectionString = ConfigurationManager.ConnectionStrings["JHDB"]?.ConnectionString;
            _amesConnectionString = ConfigurationManager.ConnectionStrings["EVERSUN_AMESDB"]?.ConnectionString;
            
            // 讀取 Schema 設定
            _schemaJhdb = ConfigurationManager.AppSettings["ActualTime_Schema_JHDB"] ?? "JHAMES";
            _schemaJhdbSys = ConfigurationManager.AppSettings["ActualTime_Schema_JHDB_SYS"] ?? "JHSYS";
            
            // 讀取輸出路徑
            string outputPath = ConfigurationManager.AppSettings["ActualTime_TxtOutputPath"] ?? "Output";
            if (!Path.IsPathRooted(outputPath))
            {
                outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, outputPath);
            }
            _outputPath = outputPath;
            
            // 確保輸出目錄存在
            if (!Directory.Exists(_outputPath))
            {
                Directory.CreateDirectory(_outputPath);
            }
        }

        /// <summary>
        /// 驗證連線設定
        /// </summary>
        public bool ValidateConfiguration()
        {
            if (string.IsNullOrEmpty(_jhdbConnectionString))
            {
                Logger.Instance.Error("找不到 JHDB 連線字串設定");
                return false;
            }
            
            if (string.IsNullOrEmpty(_amesConnectionString))
            {
                Logger.Instance.Error("找不到 EVERSUN_AMESDB 連線字串設定");
                return false;
            }
            
            return true;
        }

        /// <summary>
        /// 執行工時計算
        /// </summary>
        /// <param name="calcDate">計算日期</param>
        /// <returns>0=成功, 2=配置錯誤, 3=執行例外</returns>
        public int Execute(string calcDate)
        {
            try
            {
                if (string.IsNullOrEmpty(calcDate))
                {
                    calcDate = DateTime.Today.AddDays(-3).ToString("yyyy-MM-dd");
                }

                Logger.Instance.Info($"開始計算工時資料，計算日期: {calcDate}");

                // 計算時間範圍
                DateTime date = DateTime.Parse(calcDate);
                DateTime startDateTime = date.AddHours(0).AddMinutes(10).AddSeconds(0);
                DateTime endDateTime = date.AddDays(1).AddHours(0).AddMinutes(10).AddSeconds(0);

                Logger.Instance.Info($"CloseTime 範圍: {startDateTime:yyyy-MM-dd HH:mm:ss} ~ {endDateTime:yyyy-MM-dd HH:mm:ss}");

                // 從 AMES_DB 讀取工時資料
                var timesheetRecords = ReadTimesheetFromAmes(startDateTime, endDateTime);
                Logger.Instance.Info($"從 AMES_DB 讀取到 {timesheetRecords.Count} 筆工時資料");

                if (timesheetRecords.Count == 0)
                {
                    Logger.Instance.Warning("沒有需要處理的工時資料");
                    return 0;
                }

                // 按製程分組處理
                var unitGroups = timesheetRecords.Select(s => s.UNIT_NO).Distinct();
                foreach (var unitNo in unitGroups)
                {
                    ProcessUnitData(timesheetRecords, unitNo, calcDate);
                }

                // 產生 SAP 上傳檔案
                GenerateSapFile(calcDate);

                Logger.Instance.Info("工時計算完成");
                return 0;
            }
            catch (Exception ex)
            {
                Logger.Instance.Exception(ex, "工時計算過程中發生例外");
                return 3;
            }
        }

        /// <summary>
        /// 從 AMES_DB 讀取工時資料
        /// </summary>
        private List<TimesheetRecord> ReadTimesheetFromAmes(DateTime startDateTime, DateTime endDateTime)
        {
            using (var conn = new SqlConnection(_amesConnectionString))
            {
                string sql = @"
                    SELECT a.TIMESHEET_ID, a.WO_NO, ISNULL(b.ENG_SR, a.ENG_SR) as ENG_SR, 
                           a.UNIT_NO, c.UNIT_NAME, a.LINE_ID, d.LINE_NAME, 
                           a.STATION_ID, e.STATION_NAME, e.TEST_TYPE, a.SIDE, a.OP_CNT, 
                           a.OPEN_TIME, a.CLOSE_TIME, a.PRODUCTION_QTY, a.TOTAL_CT, a.MEMO, 
                           f.USER_NAME as OPEN_USER, g.USER_NAME as CLOSE_USER,
                           ISNULL((SELECT TOP 1 CT FROM JH_STANDARD_WORKTIME h 
                                   WHERE a.ENG_SR = h.ITEM_NO AND a.UNIT_NO = h.UNIT_NO 
                                   AND a.LINE_ID = h.LINE_ID AND a.STATION_ID = h.STATION_ID 
                                   AND a.SIDE = h.SIDE), 0) CT,
                           ISNULL((SELECT TOP 1 OP_CNT FROM JH_STANDARD_WORKTIME h 
                                   WHERE a.ENG_SR = h.ITEM_NO AND a.UNIT_NO = h.UNIT_NO 
                                   AND a.LINE_ID = h.LINE_ID AND a.STATION_ID = h.STATION_ID 
                                   AND a.SIDE = h.SIDE), 0) STD_OP
                    FROM JH_WO_TIMESHEET a
                    LEFT JOIN JH_WO_INFO b ON a.WO_NO = b.WO_NO
                    LEFT JOIN JH_USER f ON a.CREATE_USERID = f.USER_ID
                    LEFT JOIN JH_USER g ON a.UPDATE_USERID = g.USER_ID,
                    JH_FACTORY_UNIT c, JH_LINE d, JH_STATION e
                    WHERE a.UNIT_NO = c.UNIT_NO AND a.LINE_ID = d.LINE_ID AND a.STATION_ID = e.STATION_ID
                    AND a.UNIT_NO IN ('P','S','T','D','B') AND e.TEST_TYPE IS NOT NULL
                    AND CONVERT(VARCHAR(19), a.CLOSE_TIME, 120) >= @startDateTime
                    AND CONVERT(VARCHAR(19), a.CLOSE_TIME, 120) < @endDateTime";

                var parameters = new DynamicParameters();
                parameters.Add("startDateTime", startDateTime.ToString("yyyy-MM-dd HH:mm:ss"), DbType.AnsiString);
                parameters.Add("endDateTime", endDateTime.ToString("yyyy-MM-dd HH:mm:ss"), DbType.AnsiString);

                return conn.Query<TimesheetRecord>(sql, parameters).ToList();
            }
        }

        /// <summary>
        /// 處理單一製程的資料
        /// </summary>
        private void ProcessUnitData(List<TimesheetRecord> allRecords, string unitNo, string calcDate)
        {
            string route = GetRoute(unitNo);
            var unitData = allRecords.Where(w => w.UNIT_NO == unitNo && w.TEST_TYPE == route).ToList();

            if (unitData.Count == 0)
            {
                Logger.Instance.Debug($"製程 {unitNo} 沒有符合 ROUTE {route} 的資料");
                return;
            }

            var wipGroups = unitData.Select(s => s.WO_NO).Distinct();
            Logger.Instance.Info($"製程 {unitNo} (ROUTE: {route}) 共有 {wipGroups.Count()} 張工單");

            foreach (var wipNo in wipGroups)
            {
                ProcessWipData(unitData.Where(w => w.WO_NO == wipNo).ToList(), wipNo, unitNo, calcDate, route);
            }
        }

        /// <summary>
        /// 處理單一工單資料
        /// </summary>
        private void ProcessWipData(List<TimesheetRecord> wipData, string wipNo, string unitNo, string calcDate, string route)
        {
            try
            {
                int finishStationId = GetFinishStationId(unitNo);
                double productionTime = 0;
                int finishQty;

                // 計算完成數量
                if (unitNo == "P")
                {
                    finishQty = wipData.Where(w => w.STATION_ID == 213 || w.STATION_ID == 212).Sum(s => s.PRODUCTION_QTY);
                }
                else
                {
                    finishQty = wipData.Where(w => w.STATION_ID == finishStationId).Sum(s => s.PRODUCTION_QTY);
                }

                // 取得報工ID
                int actualId = GetNextId("ACTUAL_ID");

                var detailList = new List<ActualTimeDetail>();

                // 計算工時並建立明細
                foreach (var data in wipData)
                {
                    int opCnt = data.OP_CNT;
                    if (unitNo == "S")
                    {
                        productionTime += data.TOTAL_CT * data.PRODUCTION_QTY;
                        opCnt = 1;
                    }
                    else
                    {
                        productionTime += data.TOTAL_CT * data.OP_CNT * data.PRODUCTION_QTY;
                    }

                    detailList.Add(new ActualTimeDetail
                    {
                        ACTUAL_ID = actualId,
                        ACTUAL_DETAIL_ID = detailList.Count,
                        WIP_NO = wipNo,
                        BARCODE_ID = data.TIMESHEET_ID,
                        USER_ID = 0,
                        STATION_ID = data.STATION_ID,
                        UNIT_NO = unitNo,
                        PASS_DATETIME_S = data.OPEN_TIME,
                        PASS_DATETIME = data.CLOSE_TIME,
                        A_CNT = opCnt,
                        A_CT = data.TOTAL_CT.ToString(),
                        S_CT = data.PRODUCTION_QTY.ToString(),
                        LAST_STATION_ID = finishStationId,
                        CREATE_DATETIME = DateTime.Now,
                        REST_CT = "0",
                        ROUTE = route
                    });
                }

                // 建立報工主表
                var actualTime = new ActualTime
                {
                    ACTUAL_ID = actualId,
                    ACTUAL_DATE = Convert.ToDateTime(calcDate),
                    CREATE_DATE = DateTime.Now,
                    WIP_NO = wipNo,
                    UNIT_NO = unitNo,
                    PRODUCTION_TIME = productionTime.ToString("0.00"),
                    PRODUCTION_CNT = finishQty,
                    ROUTE = route,
                    SOURCE = "1",
                    PRODUCTION_CNT_SAP = finishQty
                };

                Logger.Instance.Info($"計算 {route} 工單: {wipNo} 工時: {productionTime:0.00} 完成數: {finishQty}");

                // 寫入資料庫
                InsertActualTime(actualTime);
                InsertActualTimeDetail(detailList);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"處理工單 {wipNo} 失敗", ex);
            }
        }

        /// <summary>
        /// 取得下一個流水號
        /// </summary>
        private int GetNextId(string idName)
        {
            using (var conn = new OracleConnection(_jhdbConnectionString))
            {
                conn.Open();

                // 更新流水號
                string updateSql = $"UPDATE {_schemaJhdbSys}.IDKEY SET CURRENT_NUM = CURRENT_NUM + DELTA_NUM WHERE ID_NAME = :ID_NAME";
                var param = new DynamicParameters();
                param.Add("ID_NAME", idName, DbType.AnsiString);
                int affected = conn.Execute(updateSql, param);

                if (affected > 0)
                {
                    // 取得更新後的流水號
                    string selectSql = $"SELECT CURRENT_NUM FROM {_schemaJhdbSys}.IDKEY WHERE ID_NAME = :ID_NAME";
                    return conn.QuerySingle<int>(selectSql, param);
                }
                else
                {
                    // 新增流水號記錄
                    string insertSql = $@"INSERT INTO {_schemaJhdbSys}.IDKEY 
                        (ID_NAME, CURRENT_NUM, START_NUM, LIMIT_NUM, DELTA_NUM, CREATE_DATETIME, UPDATE_DATETIME)
                        VALUES (:ID_NAME, 1000, 1000, 2147483647, 1, SYSDATE, SYSDATE)";
                    conn.Execute(insertSql, new { ID_NAME = idName });
                    return 1000;
                }
            }
        }

        /// <summary>
        /// 寫入報工主表
        /// </summary>
        private void InsertActualTime(ActualTime data)
        {
            using (var conn = new OracleConnection(_jhdbConnectionString))
            {
                string sql = $@"BEGIN 
                    INSERT INTO {_schemaJhdb}.ACTUAL_TIME 
                    (ACTUAL_ID, ACTUAL_DATE, WIP_NO, UNIT_NO, PRODUCTION_TIME, CREATE_DATE, PRODUCTION_CNT, ROUTE, SOURCE, PRODUCTION_CNT_SAP)
                    VALUES (:ACTUAL_ID, :ACTUAL_DATE, :WIP_NO, :UNIT_NO, :PRODUCTION_TIME, :CREATE_DATE, :PRODUCTION_CNT, :ROUTE, :SOURCE, :PRODUCTION_CNT_SAP);
                    EXCEPTION WHEN DUP_VAL_ON_INDEX THEN NULL;
                    END;";
                
                int affected = conn.Execute(sql, data);
                Logger.Instance.Debug($"Insert ACTUAL_TIME: {affected} records");
            }
        }

        /// <summary>
        /// 寫入報工明細
        /// </summary>
        private void InsertActualTimeDetail(List<ActualTimeDetail> dataList)
        {
            using (var conn = new OracleConnection(_jhdbConnectionString))
            {
                string sql = $@"BEGIN
                    INSERT INTO {_schemaJhdb}.ACTUAL_TIME_DETAIL 
                    (ACTUAL_ID, ACTUAL_DETAIL_ID, WIP_NO, BARCODE_ID, USER_ID, STATION_ID, UNIT_NO, PASS_DATETIME,
                     A_CNT, A_CT, S_CT, LAST_STATION_ID, CREATE_DATETIME, REST_CT, ROUTE, PASS_DATETIME_S)
                    VALUES (:ACTUAL_ID, :ACTUAL_DETAIL_ID, :WIP_NO, :BARCODE_ID, :USER_ID, :STATION_ID, :UNIT_NO, :PASS_DATETIME,
                            :A_CNT, :A_CT, :S_CT, :LAST_STATION_ID, :CREATE_DATETIME, :REST_CT, :ROUTE, :PASS_DATETIME_S);
                    EXCEPTION WHEN DUP_VAL_ON_INDEX THEN NULL;
                    END;";
                
                int affected = conn.Execute(sql, dataList);
                Logger.Instance.Debug($"Insert ACTUAL_TIME_DETAIL: {affected} records");
            }
        }

        /// <summary>
        /// 產生 SAP 上傳檔案
        /// </summary>
        private void GenerateSapFile(string calcDate)
        {
            try
            {
                string fileName = $"SFIS_WorkTime_{DateTime.Now:yyyyMMddHHmmss}.txt";
                string filePath = Path.Combine(_outputPath, fileName);

                using (var conn = new OracleConnection(_jhdbConnectionString))
                {
                    string sql = $@"SELECT WIP_NO, SUM(PRODUCTION_TIME) PRODUCTION_TIME, SUM(PRODUCTION_CNT_SAP) PRODUCTION_CNT, ROUTE
                        FROM {_schemaJhdb}.ACTUAL_TIME
                        WHERE TO_CHAR(ACTUAL_DATE, 'yyyy-MM-dd') = :calcDate
                        GROUP BY WIP_NO, ROUTE";

                    var param = new DynamicParameters();
                    param.Add("calcDate", calcDate, DbType.AnsiString);

                    var data = conn.Query(sql, param).ToList();

                    using (var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8))
                    {
                        foreach (var item in data)
                        {
                            string line = $"{calcDate.Replace("-", "")},235959,{item.WIP_NO},{item.ROUTE},{item.PRODUCTION_TIME},{item.PRODUCTION_CNT}";
                            writer.WriteLine(line);
                            Logger.Instance.Debug($"SAP: {line}");
                        }
                    }

                    Logger.Instance.Info($"SAP 上傳檔案已產生: {filePath} (共 {data.Count} 筆)");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("產生 SAP 上傳檔案失敗", ex);
            }
        }

        /// <summary>
        /// 取得製程對應的 ROUTE
        /// </summary>
        private string GetRoute(string unitNo)
        {
            switch (unitNo)
            {
                case "S": return "0010"; // SMT
                case "D": return "0020"; // 前段
                case "T": return "0020"; // 測試
                case "P": return "0020"; // 包裝
                case "B": return "0030"; // 組裝
                default: return "";
            }
        }

        /// <summary>
        /// 取得製程對應的最後站別ID
        /// </summary>
        private int GetFinishStationId(string unitNo)
        {
            switch (unitNo)
            {
                case "S": return 12;   // SMT_TOP
                case "T": return 229;  // 送驗品保
                case "B": return 37;   // PACKING
                default: return 0;
            }
        }
    }
}
