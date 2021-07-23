namespace Fcm.Models
{
    public class AppNamespace
    {
        public int AppId { get; set; }

        public int GroupId { get; set; }

        public string Name { get; set; }

        public long CreateAt { get; set; }

        public Role Role { get; set; }

        public bool IsRefer { get; set; }

        public int ReferId { get; set; }
    }
}
