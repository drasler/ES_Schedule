namespace ES_Schedule.Models
{
    /// <summary>
    /// Email 收件人資料模型
    /// </summary>
    public class MailRecipient
    {
        /// <summary>
        /// 使用者ID
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// 使用者名稱
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 使用者 Email
        /// </summary>
        public string UserEmail { get; set; }
    }
}
