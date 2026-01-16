using System;
using System.Collections.Generic;
using ES_Schedule.Interfaces;
using ES_Schedule.Jobs;

namespace ES_Schedule.Services
{
    /// <summary>
    /// 任務工廠類別，負責建立與管理排程任務
    /// </summary>
    public static class JobFactory
    {
        // 已註冊的排程任務
        private static readonly Dictionary<string, Func<IScheduleJob>> _jobs 
            = new Dictionary<string, Func<IScheduleJob>>(StringComparer.OrdinalIgnoreCase)
        {
            { "RepairRecordsTransfer", () => new RepairRecordsTransferJob() },
            { "ActualTimeCalc", () => new ActualTimeCalcJob() },
            { "SMT_Stencil_Overdue", () => new StencilOverdueJob() },
            // 未來新增其他任務只需在此註冊
        };

        /// <summary>
        /// 根據任務名稱建立對應的排程任務實例
        /// </summary>
        /// <param name="jobName">任務名稱</param>
        /// <returns>排程任務實例，若找不到則回傳 null</returns>
        public static IScheduleJob CreateJob(string jobName)
        {
            if (_jobs.TryGetValue(jobName, out var factory))
                return factory();
            return null;
        }

        /// <summary>
        /// 取得所有可用的排程任務名稱
        /// </summary>
        /// <returns>任務名稱集合</returns>
        public static IEnumerable<string> GetAvailableJobs() => _jobs.Keys;
    }
}
