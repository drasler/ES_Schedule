using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using Dapper;
using Oracle.ManagedDataAccess.Client;
using ES_Schedule.Common;
using ES_Schedule.Models;

namespace ES_Schedule.Services
{
    /// <summary>
    /// 鋼板逾期檢查服務
    /// </summary>
    public class StencilOverdueService
    {
        private readonly string _jhdbConnectionString;
        private readonly int _daysOnlineThreshold;
        private readonly decimal _usageRateThreshold;
        private readonly string _mailGroupNo;
        private readonly bool _testMode;
        private readonly string _testRecipient;
        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly string _senderEmail;

        public StencilOverdueService()
        {
            // 讀取連線字串
            _jhdbConnectionString = ConfigurationManager.ConnectionStrings["JHDB"]?.ConnectionString;

            // 讀取配置
            _daysOnlineThreshold = int.TryParse(ConfigurationManager.AppSettings["StencilOverdue_DaysOnlineThreshold"], out int days) ? days : 7;
            _usageRateThreshold = decimal.TryParse(ConfigurationManager.AppSettings["StencilOverdue_UsageRateThreshold"], out decimal rate) ? rate : 0.95m;
            _mailGroupNo = ConfigurationManager.AppSettings["StencilOverdue_MailGroupNo"] ?? "STEEL_ALARM";
            _testMode = bool.TryParse(ConfigurationManager.AppSettings["StencilOverdue_TestMode"], out bool test) && test;
            _testRecipient = ConfigurationManager.AppSettings["StencilOverdue_TestRecipient"] ?? "daloz.e@eversun.com.tw";

            // SMTP 設定
            _smtpServer = ConfigurationManager.AppSettings["Mail_SmtpServer"] ?? "mail.eversun.com.tw";
            _smtpPort = int.TryParse(ConfigurationManager.AppSettings["Mail_SmtpPort"], out int port) ? port : 25;
            _senderEmail = ConfigurationManager.AppSettings["Mail_SenderEmail"] ?? "ames@eversun.com.tw";

            Logger.Instance.Info($"配置載入完成 - 在線逾時天數: {_daysOnlineThreshold}, 使用率閾值: {_usageRateThreshold:P0}, MailGroup: {_mailGroupNo}, 測試模式: {_testMode}");
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
            return true;
        }

        /// <summary>
        /// 執行逾期檢查
        /// </summary>
        /// <returns>0=成功, 2=配置錯誤, 3=執行例外</returns>
        public int Execute()
        {
            try
            {
                Logger.Instance.Info("開始查詢逾期鋼板 (排除已下線)...");

                // 1. 查詢逾期鋼板
                var overdueRecords = GetOverdueStencils();

                if (overdueRecords == null || overdueRecords.Count == 0)
                {
                    Logger.Instance.Info("查詢完成：無逾期鋼板");
                    return 0;
                }

                // 統計各層級數量
                var warningCount = overdueRecords.Count(r => r.AlertLevel == "警告");
                var severeCount = overdueRecords.Count(r => r.AlertLevel == "嚴重");
                var urgentCount = overdueRecords.Count(r => r.AlertLevel == "緊急");

                Logger.Instance.Info($"發現 {overdueRecords.Count} 筆逾期鋼板 (警告: {warningCount}, 嚴重: {severeCount}, 緊急: {urgentCount})");

                // 2. 取得收件人
                var recipients = GetMailRecipients();
                if (recipients == null || recipients.Count == 0)
                {
                    Logger.Instance.Warning($"找不到 {_mailGroupNo} 群組的收件人，中止發送");
                    return 0;
                }
                Logger.Instance.Info($"取得 {_mailGroupNo} 群組收件人: {recipients.Count} 人");

                // 3. 產生 Email HTML
                string emailBody = GenerateEmailBody(overdueRecords);

                // 4. 發送 Email
                bool sendResult = SendEmail(recipients, emailBody);
                if (!sendResult)
                {
                    Logger.Instance.Error("Email 發送失敗");
                    return 3;
                }

                // 5. 更新告警旗標 (僅針對使用率達95%且尚未通知的)
                UpdateAlertFlags(overdueRecords);

                Logger.Instance.Info("排程任務完成");
                return 0;
            }
            catch (Exception ex)
            {
                Logger.Instance.Exception(ex, "鋼板逾期檢查過程中發生例外");
                return 3;
            }
        }

        /// <summary>
        /// 查詢逾期鋼板
        /// </summary>
        private List<StencilOverdueRecord> GetOverdueStencils()
        {
            string sql = $@"
SELECT 
    I.STEEL_PLATE_ID AS SteelPlateId,
    I.STEEL_PLATE_NO AS SteelPlateNo,
    I.ITEMS AS EngNo,
    I.STORAGE_LOCATION AS StorageLocation,
    I.USED_TIMES AS MaxUseTimes,
    I.BE_USE_TIMES AS UsedTimes,
    ROUND(I.BE_USE_TIMES / NULLIF(I.USED_TIMES, 0) * 100, 1) AS UsageRate,
    I.USAGE_FREQUENCY_ALERT AS IsAlerted,
    M.WIP_NO AS CurrentWipNo,
    TO_CHAR(M.ON_DATE, 'YYYY/MM/DD') AS OnlineDate,
    M.ON_DATE AS OnlineDateRaw,
    TRUNC(SYSDATE - M.ON_DATE) AS DaysOnline,
    U.USER_NAME AS CreateBy,
    CASE 
        WHEN I.BE_USE_TIMES >= I.USED_TIMES THEN '嚴重'
        WHEN M.ON_DATE IS NOT NULL AND M.OFF_DATE IS NULL AND SYSDATE - M.ON_DATE > :daysThreshold THEN '緊急'
        WHEN I.BE_USE_TIMES / NULLIF(I.USED_TIMES, 0) >= :usageThreshold AND NVL(I.USAGE_FREQUENCY_ALERT, 'N') = 'N' THEN '警告'
        ELSE '正常'
    END AS AlertLevel,
    CASE 
        WHEN I.BE_USE_TIMES >= I.USED_TIMES THEN '已達使用上限'
        WHEN M.ON_DATE IS NOT NULL AND M.OFF_DATE IS NULL AND SYSDATE - M.ON_DATE > :daysThreshold THEN '在線超過' || :daysThreshold || '天'
        WHEN I.BE_USE_TIMES / NULLIF(I.USED_TIMES, 0) >= :usageThreshold AND NVL(I.USAGE_FREQUENCY_ALERT, 'N') = 'N' THEN '使用率達95%'
        ELSE '-'
    END AS OverdueReason
FROM JHAMES.STEEL_PLATE_INFO I
LEFT JOIN (
    SELECT STEEL_PLATE_ID, WIP_NO, ON_DATE, OFF_DATE
    FROM JHAMES.STEEL_PLATE_MEASURE
    WHERE (STEEL_PLATE_ID, ON_DATE) IN (
        SELECT STEEL_PLATE_ID, MAX(ON_DATE)
        FROM JHAMES.STEEL_PLATE_MEASURE
        GROUP BY STEEL_PLATE_ID
    )
) M ON I.STEEL_PLATE_ID = M.STEEL_PLATE_ID
LEFT JOIN JHSYS.USER_INFO U ON I.CREATE_USERID = U.USER_ID
WHERE I.STATUS = '1'
AND (M.OFF_DATE IS NULL OR M.ON_DATE IS NULL)
AND (
    (I.BE_USE_TIMES / NULLIF(I.USED_TIMES, 0) >= :usageThreshold AND NVL(I.USAGE_FREQUENCY_ALERT, 'N') = 'N')
    OR (I.BE_USE_TIMES >= I.USED_TIMES)
    OR (M.ON_DATE IS NOT NULL AND M.OFF_DATE IS NULL AND SYSDATE - M.ON_DATE > :daysThreshold)
)
ORDER BY 
    CASE 
        WHEN I.BE_USE_TIMES >= I.USED_TIMES THEN 2
        WHEN M.ON_DATE IS NOT NULL AND M.OFF_DATE IS NULL AND SYSDATE - M.ON_DATE > :daysThreshold THEN 1
        ELSE 3
    END,
    I.BE_USE_TIMES / NULLIF(I.USED_TIMES, 0) DESC NULLS LAST";

            using (var conn = new OracleConnection(_jhdbConnectionString))
            {
                var param = new DynamicParameters();
                param.Add("daysThreshold", _daysOnlineThreshold, DbType.Int32);
                param.Add("usageThreshold", _usageRateThreshold, DbType.Decimal);

                Logger.Instance.Debug($"執行逾期鋼板查詢 (daysThreshold={_daysOnlineThreshold}, usageThreshold={_usageRateThreshold})");
                return conn.Query<StencilOverdueRecord>(sql, param).ToList();
            }
        }

        /// <summary>
        /// 取得 Mail Group 收件人
        /// </summary>
        private List<MailRecipient> GetMailRecipients()
        {
            string sql = @"
SELECT 
    U.USER_ID AS UserId,
    U.USER_NAME AS UserName,
    U.USER_EMAIL AS UserEmail
FROM JHAMES.MAIL_GROUP G
INNER JOIN JHAMES.MAIL_GROUP_DETAIL D ON G.GROUP_ID = D.GROUP_ID
INNER JOIN JHSYS.USER_INFO U ON D.USER_ID = U.USER_ID
WHERE G.GROUP_NO = :groupNo
AND U.USER_STATUSID = 1
AND U.USER_EMAIL IS NOT NULL
ORDER BY U.USER_NAME";

            using (var conn = new OracleConnection(_jhdbConnectionString))
            {
                var param = new DynamicParameters();
                param.Add("groupNo", _mailGroupNo, DbType.AnsiString);

                return conn.Query<MailRecipient>(sql, param).ToList();
            }
        }

        /// <summary>
        /// 產生 Email HTML 內容
        /// </summary>
        private string GenerateEmailBody(List<StencilOverdueRecord> records)
        {
            var sb = new StringBuilder();
            string checkDate = DateTime.Now.ToString("yyyy/MM/dd");

            // 統計
            var warningCount = records.Count(r => r.AlertLevel == "警告");
            var severeCount = records.Count(r => r.AlertLevel == "嚴重");
            var urgentCount = records.Count(r => r.AlertLevel == "緊急");

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head><meta charset=\"UTF-8\"></head>");
            sb.AppendLine("<body style=\"font-family: 'Microsoft JhengHei', 'Segoe UI', sans-serif; padding: 20px;\">");

            // Header
            sb.AppendLine("<div style=\"background: linear-gradient(135deg, #3b82f6, #8b5cf6); color: white; padding: 20px; border-radius: 10px;\">");
            sb.AppendLine("<h2 style=\"margin: 0;\">🚨 [AMES系統通知] SMT 鋼板逾期告警</h2>");
            sb.AppendLine("<p style=\"margin: 5px 0 0 0; opacity: 0.9;\">Stencil Overdue Alert Notification</p>");
            sb.AppendLine("</div>");

            // Body
            sb.AppendLine("<div style=\"background: white; padding: 20px; margin-top: 15px;\">");
            sb.AppendLine("<p><strong>Hi~All,</strong></p>");
            sb.AppendLine("<p>系統偵測到以下鋼板已達逾期條件，請儘速安排處理以避免影響生產線運作。</p>");

            // 統計區
            sb.AppendLine("<div style=\"background: #fef3c7; padding: 15px; border-radius: 8px; margin: 20px 0; border-left: 4px solid #f59e0b;\">");
            sb.AppendLine("<p style=\"margin: 0;\"><strong>📊 統計資訊</strong></p>");
            sb.AppendLine($"<p style=\"margin: 5px 0 0 0;\">• 檢查日期：<strong>{checkDate}</strong></p>");
            sb.AppendLine($"<p style=\"margin: 5px 0 0 0;\">• 逾期數量：<strong>{records.Count} 筆</strong>（警告: {warningCount}, 嚴重: {severeCount}, 緊急: {urgentCount}）</p>");
            sb.AppendLine("</div>");

            // 表格
            sb.AppendLine("<h4 style=\"margin: 25px 0 15px 0;\">📋 逾期鋼板清單</h4>");
            sb.AppendLine("<table style=\"width: 100%; border-collapse: collapse; font-size: 13px;\">");
            sb.AppendLine("<thead><tr style=\"background: #eff6ff;\">");
            sb.AppendLine("<th style=\"padding: 10px; border: 1px solid #cbd5e1; text-align: left;\">層級</th>");
            sb.AppendLine("<th style=\"padding: 10px; border: 1px solid #cbd5e1; text-align: left;\">鋼板編號</th>");
            sb.AppendLine("<th style=\"padding: 10px; border: 1px solid #cbd5e1; text-align: left;\">工程編號</th>");
            sb.AppendLine("<th style=\"padding: 10px; border: 1px solid #cbd5e1; text-align: center;\">使用率</th>");
            sb.AppendLine("<th style=\"padding: 10px; border: 1px solid #cbd5e1; text-align: center;\">已用/可用</th>");
            sb.AppendLine("<th style=\"padding: 10px; border: 1px solid #cbd5e1; text-align: left;\">逾期原因</th>");
            sb.AppendLine("<th style=\"padding: 10px; border: 1px solid #cbd5e1; text-align: left;\">上線日期</th>");
            sb.AppendLine("<th style=\"padding: 10px; border: 1px solid #cbd5e1; text-align: left;\">儲位</th>");
            sb.AppendLine("</tr></thead>");
            sb.AppendLine("<tbody>");

            foreach (var record in records)
            {
                string rowStyle = record.AlertLevel == "緊急" ? "background: #fee2e2;" :
                                  record.AlertLevel == "嚴重" ? "background: #fed7aa;" :
                                  record.AlertLevel == "警告" ? "background: #fef3c7;" : "";

                string levelIcon = record.AlertLevel == "緊急" ? "🔴 緊急" :
                                   record.AlertLevel == "嚴重" ? "🟠 嚴重" :
                                   record.AlertLevel == "警告" ? "🟡 警告" : record.AlertLevel;

                string levelColor = record.AlertLevel == "緊急" ? "#dc2626" :
                                    record.AlertLevel == "嚴重" ? "#ea580c" :
                                    record.AlertLevel == "警告" ? "#ca8a04" : "#374151";

                string usageRateDisplay = record.UsageRate > 0 ? $"{record.UsageRate:0.0}%" : "-";
                string daysInfo = record.DaysOnline.HasValue && record.AlertLevel == "緊急" 
                    ? $" ({record.DaysOnline}天)" : "";

                // 工程編號超過20碼則截斷
                string engNoDisplay = record.EngNo ?? "-";
                if (!string.IsNullOrEmpty(record.EngNo) && record.EngNo.Length > 20)
                {
                    engNoDisplay = record.EngNo.Substring(0, 20) + "...";
                }

                sb.AppendLine($"<tr style=\"{rowStyle}\">");
                sb.AppendLine($"<td style=\"padding: 10px; border: 1px solid #cbd5e1;\"><strong style=\"color: {levelColor};\">{levelIcon}</strong></td>");
                sb.AppendLine($"<td style=\"padding: 10px; border: 1px solid #cbd5e1;\">{record.SteelPlateNo}</td>");
                sb.AppendLine($"<td style=\"padding: 10px; border: 1px solid #cbd5e1;\">{engNoDisplay}</td>");
                sb.AppendLine($"<td style=\"padding: 10px; border: 1px solid #cbd5e1; text-align: center;\">{usageRateDisplay}</td>");
                sb.AppendLine($"<td style=\"padding: 10px; border: 1px solid #cbd5e1; text-align: center;\">{record.UsedTimes} / {record.MaxUseTimes}</td>");
                sb.AppendLine($"<td style=\"padding: 10px; border: 1px solid #cbd5e1;\">{record.OverdueReason}{daysInfo}</td>");
                sb.AppendLine($"<td style=\"padding: 10px; border: 1px solid #cbd5e1;\">{record.OnlineDate ?? "-"}</td>");
                sb.AppendLine($"<td style=\"padding: 10px; border: 1px solid #cbd5e1;\">{record.StorageLocation ?? "-"}</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody></table>");

            // 處理建議
            sb.AppendLine("<div style=\"background: #dbeafe; padding: 15px; border-radius: 8px; margin: 25px 0; border-left: 4px solid #3b82f6;\">");
            sb.AppendLine("<p style=\"margin: 0;\"><strong>💡 處理建議</strong></p>");
            sb.AppendLine("<p style=\"margin: 5px 0 0 0;\">• <strong>緊急/嚴重</strong>：請立即安排鋼板更換作業</p>");
            sb.AppendLine("<p style=\"margin: 5px 0 0 0;\">• <strong>警告</strong>：請提前準備新鋼板，避免影響生產排程</p>");
            sb.AppendLine("</div>");

            // Footer
            sb.AppendLine("<hr style=\"border: none; border-top: 1px solid #e5e7eb; margin: 25px 0;\">");
            sb.AppendLine("<p style=\"font-size: 12px; color: #6b7280;\">此為系統自動發送的通知郵件，如有疑問請聯繫 IT 部門。<br/>");
            sb.AppendLine("查詢路徑：AMES系統 → PCB管理 → PCB016 鋼板量測記錄</p>");
            sb.AppendLine("<p style=\"font-size: 11px; color: #9ca3af; margin-top: 15px;\">");
            sb.AppendLine("<em>Powered by EverSun | Daloz(ㄚ智)</em></p>");

            sb.AppendLine("</div></body></html>");

            return sb.ToString();
        }

        /// <summary>
        /// 發送 Email
        /// </summary>
        private bool SendEmail(List<MailRecipient> recipients, string body)
        {
            try
            {
                string subject = "[AMES系統通知] SMT 鋼板逾期告警";
                string toEmails;

                if (_testMode)
                {
                    toEmails = _testRecipient;
                    subject = "[測試] " + subject;
                    Logger.Instance.Info($"測試模式：Email 將發送給 {_testRecipient}");
                }
                else
                {
                    toEmails = string.Join(",", recipients.Select(r => r.UserEmail));
                }

                using (var mail = new MailMessage())
                {
                    mail.From = new MailAddress(_senderEmail, "AMES系統");
                    
                    foreach (var email in toEmails.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        mail.To.Add(email.Trim());
                    }

                    mail.Subject = subject;
                    mail.Body = body;
                    mail.IsBodyHtml = true;
                    mail.BodyEncoding = Encoding.UTF8;
                    mail.SubjectEncoding = Encoding.UTF8;

                    using (var smtp = new SmtpClient(_smtpServer, _smtpPort))
                    {
                        smtp.EnableSsl = false;
                        smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                        smtp.Send(mail);
                    }
                }

                Logger.Instance.Info($"發送 Email 成功 - 收件人: {toEmails}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Instance.Exception(ex, "發送 Email 失敗");
                return false;
            }
        }

        /// <summary>
        /// 更新告警旗標
        /// </summary>
        private void UpdateAlertFlags(List<StencilOverdueRecord> records)
        {
            // 僅針對「使用率達95%」且尚未通知的更新
            var toUpdate = records.Where(r => 
                r.AlertLevel == "警告" && 
                r.OverdueReason.Contains("95%") &&
                (r.IsAlerted == "N" || string.IsNullOrEmpty(r.IsAlerted))
            ).ToList();

            if (toUpdate.Count == 0)
            {
                Logger.Instance.Debug("無需更新告警旗標");
                return;
            }

            string sql = @"
UPDATE JHAMES.STEEL_PLATE_INFO 
SET USAGE_FREQUENCY_ALERT = 'Y',
    UPDATE_DATE = SYSDATE,
    UPDATE_USERID = 0
WHERE STEEL_PLATE_ID = :SteelPlateId
AND NVL(USAGE_FREQUENCY_ALERT, 'N') = 'N'";

            using (var conn = new OracleConnection(_jhdbConnectionString))
            {
                conn.Open();
                foreach (var record in toUpdate)
                {
                    try
                    {
                        var param = new DynamicParameters();
                        param.Add("SteelPlateId", record.SteelPlateId, DbType.Int32);
                        
                        int affected = conn.Execute(sql, param);
                        
                        if (affected > 0)
                        {
                            // 記錄旗標變更 Log
                            Logger.Instance.Info($"更新旗標 USAGE_FREQUENCY_ALERT - 鋼板編號: {record.SteelPlateNo}, 工程編號: {record.EngNo ?? "-"}, 原始值: N, 新值: Y, 使用率: {record.UsageRate:0.0}%, 觸發原因: {record.OverdueReason}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Error($"更新旗標失敗 - 鋼板: {record.SteelPlateNo}", ex);
                    }
                }
            }
        }
    }
}
