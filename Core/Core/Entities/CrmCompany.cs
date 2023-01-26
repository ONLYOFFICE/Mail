using ASC.Common.Security;
using Nest;

namespace ASC.Mail.Core.Core.Entities
{
    [Serializable]
    public class CrmCompany : CrmContact, ISecurityObjectId
    {
        public CrmCompany()
        {
            CompanyName = string.Empty;
        }

        public Type ObjectType => GetType();

        public object SecurityId => Id;

        public string FullId => "";
    }
}
