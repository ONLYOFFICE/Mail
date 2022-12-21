using ASC.Common.Mapping;
using ASC.Common.Security;
using Nest;
using ASC.Mail.Core.Enums;

namespace ASC.Mail.Core.Core.Entities
{
    public class RelationshipEvent : CrmDomainObject, ISecurityObjectId
    {
        public Guid CreateBy { get; set; }

        public DateTime CreateOn { get; set; }

        public Guid? LastModifedBy { get; set; }

        public DateTime? LastModifedOn { get; set; }

        public string Content { get; set; }

        public int ContactID { get; set; }

        public EntityType EntityType { get; set; }

        public int EntityID { get; set; }

        public int CategoryID { get; set; }

        public object SecurityId => ID;

        public string FullId => AzObjectIdHelper.GetFullObjectId(this);

        public Type ObjectType => GetType();
    }
}
