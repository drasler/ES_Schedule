using System;

namespace ES_Schedule.Models
{
    /// <summary>
    /// 工時記錄模型 - 對應 AMES_DB.JH_WO_TIMESHEET 查詢結果
    /// </summary>
    public class TimesheetRecord
    {
        /// <summary>
        /// 時間表ID
        /// </summary>
        public int TIMESHEET_ID { get; set; }

        /// <summary>
        /// 工單號碼
        /// </summary>
        public string WO_NO { get; set; }

        /// <summary>
        /// 工程料號
        /// </summary>
        public string ENG_SR { get; set; }

        /// <summary>
        /// 製程代碼
        /// </summary>
        public string UNIT_NO { get; set; }

        /// <summary>
        /// 製程名稱
        /// </summary>
        public string UNIT_NAME { get; set; }

        /// <summary>
        /// 線別ID
        /// </summary>
        public int LINE_ID { get; set; }

        /// <summary>
        /// 線別名稱
        /// </summary>
        public string LINE_NAME { get; set; }

        /// <summary>
        /// 站別ID
        /// </summary>
        public int STATION_ID { get; set; }

        /// <summary>
        /// 站別名稱
        /// </summary>
        public string STATION_NAME { get; set; }

        /// <summary>
        /// 測試類型 (對應 ROUTE)
        /// </summary>
        public string TEST_TYPE { get; set; }

        /// <summary>
        /// 正反面
        /// </summary>
        public string SIDE { get; set; }

        /// <summary>
        /// 作業人數
        /// </summary>
        public int OP_CNT { get; set; }

        /// <summary>
        /// 開始時間
        /// </summary>
        public DateTime OPEN_TIME { get; set; }

        /// <summary>
        /// 結束時間
        /// </summary>
        public DateTime CLOSE_TIME { get; set; }

        /// <summary>
        /// 該站完成數
        /// </summary>
        public int PRODUCTION_QTY { get; set; }

        /// <summary>
        /// 該站平均一台的工時
        /// </summary>
        public double TOTAL_CT { get; set; }

        /// <summary>
        /// 備註
        /// </summary>
        public string MEMO { get; set; }

        /// <summary>
        /// 開單人員
        /// </summary>
        public string OPEN_USER { get; set; }

        /// <summary>
        /// 結單人員
        /// </summary>
        public string CLOSE_USER { get; set; }

        /// <summary>
        /// 標準工時
        /// </summary>
        public int CT { get; set; }

        /// <summary>
        /// 標準人數
        /// </summary>
        public int STD_OP { get; set; }

        /// <summary>
        /// 效率
        /// </summary>
        public double Efficiency { get; set; }
    }
}
