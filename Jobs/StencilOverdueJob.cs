using System;
using System.Diagnostics;
using System.Threading.Tasks;
using ES_Schedule.Common;
using ES_Schedule.Interfaces;
using ES_Schedule.Services;

namespace ES_Schedule.Jobs
{
    /// <summary>
    /// SMT 鋼板逾期通知排程任務
    /// </summary>
    public class StencilOverdueJob : IScheduleJob
    {
        /// <summary>
        /// 任務名稱
        /// </summary>
        public string JobName => "SMT_Stencil_Overdue";

        /// <summary>
        /// 任務描述
        /// </summary>
        public string Description => "SMT 鋼板逾期通知排程 - 檢查鋼板使用率與在線時間，發送逾期告警 Email";

        /// <summary>
        /// 執行排程任務
        /// </summary>
        /// <returns>Exit Code: 0=成功, 1=找不到任務, 2=配置錯誤, 3=執行例外</returns>
        public async Task<int> ExecuteAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            Logger.Instance.Info($"開始執行排程任務: {JobName}");
            Logger.Instance.Info(new string('=', 60));

            try
            {
                var service = new StencilOverdueService();

                // 驗證配置
                if (!service.ValidateConfiguration())
                {
                    Logger.Instance.Error("配置驗證失敗，任務中止");
                    return 2;
                }

                // 執行核心邏輯 (同步執行，可未來改為 async)
                int result = await Task.Run(() => service.Execute());

                stopwatch.Stop();
                Logger.Instance.Info(new string('=', 60));
                Logger.Instance.Info($"排程任務完成: {JobName} (結果: {result}, 耗時: {stopwatch.Elapsed.TotalSeconds:F1} 秒)");

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.Instance.Fatal($"排程任務異常終止: {JobName}", ex);
                Logger.Instance.Info($"排程任務失敗 (耗時: {stopwatch.Elapsed.TotalSeconds:F1} 秒)");
                return 3;
            }
        }
    }
}
