using ASC.Common.Security;
using ASC.Mail.Core.Enums;

namespace ASC.Mail.Core.Core.Entities
{
    public class CrmSecurityObjectProvider : ISecurityObjectProvider
    {
        public bool InheritSupported => true;

        public bool ObjectRolesSupported => false;

        public ISecurityObjectId InheritFrom(ISecurityObjectId objectId)
        {
            int contactID;
            int entityID;
            EntityType entityType;
            if (objectId is CrmTask)
            {
                CrmTask task = (CrmTask)objectId;
                contactID = task.ContactID;
                entityID = task.EntityID;
                entityType = task.EntityType;
            }
            else
            {
                if (!(objectId is RelationshipEvent))
                {
                    return null;
                }

                RelationshipEvent relationshipEvent = (RelationshipEvent)objectId;
                contactID = relationshipEvent.ContactID;
                entityID = relationshipEvent.EntityID;
                entityType = relationshipEvent.EntityType;
            }

            if (entityID == 0 && contactID == 0)
            {
                return null;
            }

            return new CrmCompany
            {
                Id = contactID,
                CompanyName = "fakeCompany"
            };

        }

        public IEnumerable<IRole> GetObjectRoles(ISubject account, ISecurityObjectId objectId, SecurityCallContext callContext)
        {
            throw new NotImplementedException();
        }
    }
}
