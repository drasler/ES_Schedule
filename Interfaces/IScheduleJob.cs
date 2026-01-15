using System.Threading.Tasks;

namespace ES_Schedule.Interfaces
{
    /// <summary>
    /// 排程任務介面 - 所有排程任務必須實作
    /// </summary>
    public interface IScheduleJob
    {
        /// <summary>
        /// 任務識別名稱 (對應工作排程器的引數)
        /// </summary>
        string JobName { get; }
        
        /// <summary>
        /// 任務描述
        /// </summary>
        string Description { get; }
        
        /// <summary>
        /// 執行排程任務
        /// </summary>
        /// <returns>0=成功, 非0=失敗</returns>
        Task<int> ExecuteAsync();
    }
}
