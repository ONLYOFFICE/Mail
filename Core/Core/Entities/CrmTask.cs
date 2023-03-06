using ASC.Common.Security;
using ASC.Mail.Core.Enums;

namespace ASC.Mail.Core.Core.Entities
{
    public class CrmTask : CrmDomainObject, ISecurityObjectId
    {
        public Guid CreateBy { get; set; }

        public DateTime CreateOn { get; set; }

        public Guid? LastModifedBy { get; set; }

        public DateTime? LastModifedOn { get; set; }

        public int ContactID { get; set; }

        public Contact Contact { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public DateTime DeadLine { get; set; }

        public Guid ResponsibleID { get; set; }

        public bool IsClosed { get; set; }

        public int CategoryID { get; set; }

        public EntityType EntityType { get; set; }

        public int EntityID { get; set; }

        public int AlertValue { get; set; }

        public object SecurityId => ID;

        public string FullId => AzObjectIdHelper.GetFullObjectId(this);

        public Type ObjectType => GetType();
    }
}
