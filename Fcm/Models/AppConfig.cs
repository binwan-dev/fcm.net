namespace Fcm.Models
{
    public class AppConfig
    {
        public int Id { get; set; }

        public int NamespaceId { get; set; }

        public string Name { get; set; }

        public string Data { get; set; }

        public long CreateAt { get; set; }

        public long UpdateAt { get; set; }

        public ValidType ValidType { get; set; }
    }
}
