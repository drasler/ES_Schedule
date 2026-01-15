using System.Collections.Generic;

namespace ES_Schedule.Config
{
    /// <summary>
    /// 資料轉移配置類別，定義每個轉移任務的細節
    /// </summary>
    public class TransferConfig
    {
        /// <summary>
        /// 轉移任務名稱
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 轉移任務描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Oracle來源查詢SQL
        /// </summary>
        public string SourceQuery { get; set; }

        /// <summary>
        /// MSSQL目標資料表名稱
        /// </summary>
        public string DestinationTable { get; set; }

        /// <summary>
        /// 是否啟用此轉移任務
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 批次處理大小，如果為null則使用全局設定
        /// </summary>
        public int? BatchSize { get; set; }

        /// <summary>
        /// 欄位映射設定，如Oracle欄位名稱與MSSQL欄位名稱不同時使用
        /// </summary>
        public Dictionary<string, string> FieldMapping { get; set; } = new Dictionary<string, string>();
    }
}
