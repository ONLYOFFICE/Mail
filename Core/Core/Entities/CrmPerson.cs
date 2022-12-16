using ASC.Common.Security;

namespace ASC.Mail.Core.Core.Entities
{
    [Serializable]
    public class CrmPerson : ASC.Core.Common.EF.Model.CrmContact, ISecurityObjectId
    {

        public int CompanyID { get; set; }

        public string JobTitle { get; set; }

        public object SecurityId => throw new NotImplementedException();

        public Type ObjectType => throw new NotImplementedException();

        public CrmPerson()
        {
            FirstName = string.Empty;
            LastName = string.Empty;
            CompanyID = 0;
            JobTitle = string.Empty;
        }
    }
}
