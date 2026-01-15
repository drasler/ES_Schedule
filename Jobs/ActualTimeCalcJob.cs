using System;
using System.Configuration;
using System.Threading.Tasks;
using ES_Schedule.Interfaces;
using ES_Schedule.Common;
using ES_Schedule.Services;

namespace ES_Schedule.Jobs
{
    /// <summary>
    /// 工時計算排程任務
    /// 從 AMES_DB 讀取工時資料，計算後寫入 JHDB，並產生 SAP 上傳檔案
    /// </summary>
    public class ActualTimeCalcJob : IScheduleJob
    {
        /// <summary>
        /// 任務識別名稱
        /// </summary>
        public string JobName => "ActualTimeCalc";

        /// <summary>
        /// 任務描述
        /// </summary>
        public string Description => "計算工時資料並寫入 JHDB，產生 SAP 上傳檔案";

        /// <summary>
        /// 執行排程任務
        /// </summary>
        /// <returns>Exit Code: 0=成功, 2=配置錯誤, 3=執行例外</returns>
        public async Task<int> ExecuteAsync()
        {
            try
            {
                Logger.Instance.Info($"開始執行排程任務: {JobName}");

                // 建立服務實例
                var service = new ActualTimeCalcService();

                // 驗證設定
                if (!service.ValidateConfiguration())
                {
                    Logger.Instance.Error("配置驗證失敗，無法執行任務");
                    return 2; // 配置錯誤
                }

                // 讀取計算日期 (可透過 App.config 設定或使用預設值)
                string calcDate = ConfigurationManager.AppSettings["ActualTime_CalcDate"] ?? "";

                // 執行工時計算
                int result = service.Execute(calcDate);

                Logger.Instance.Info($"排程任務執行完成: {JobName}");

                // 模擬非同步處理 (保持介面一致性)
                await Task.CompletedTask;

                return result;
            }
            catch (Exception ex)
            {
                Logger.Instance.Exception(ex, $"排程任務執行失敗: {JobName}");
                return 3; // 執行例外
            }
        }
    }
}
