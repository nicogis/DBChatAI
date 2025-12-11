using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.BaseImpl.PermissionPolicy;
using DevExpress.Xpo;

namespace DBChatAI.Module.BusinessObjects.AI
{


    [DefaultClassOptions]
    public class AiChatSession : BaseObject
    {
        public AiChatSession(Session session) : base(session) { }

        private string title;
        [Size(255)]
        public string Title
        {
            get => title;
            set => SetPropertyValue(nameof(Title), ref title, value);
        }

        
        private PermissionPolicyUser owner;
        public PermissionPolicyUser Owner
        {
            get => owner;
            set => SetPropertyValue(nameof(Owner), ref owner, value);
        }

        

        [Association("AiChatSession-AiMessages")]
        public XPCollection<AiMessage> Messages => GetCollection<AiMessage>(nameof(Messages));

        public DateTime CreatedOn { get; set; } = DateTime.Now;
    }

    public enum AiMessageRole
    {
        User,
        Assistant,
        System
    }

    [DefaultClassOptions]
    public class AiMessage : BaseObject
    {
        public AiMessage(Session session) : base(session) { }

        private AiChatSession sessionObj;
        [Association("AiChatSession-AiMessages")]
        public AiChatSession AiChatSession
        {
            get => sessionObj;
            set => SetPropertyValue(nameof(AiChatSession), ref sessionObj, value);
        }

        private AiMessageRole role;
        public AiMessageRole Role
        {
            get => role;
            set => SetPropertyValue(nameof(Role), ref role, value);
        }

        [Size(SizeAttribute.Unlimited)]
        public string Content { get; set; }

        [Size(SizeAttribute.Unlimited)]
        public string SqlQuery { get; set; }

        public DateTime CreatedOn { get; set; } = DateTime.Now;
    }


}
