namespace ASC.Mail.Core.Core.Entities
{
    [Serializable]
    public class CrmPerson : ASC.Core.Common.EF.Model.CrmContact
    {
        public string FirstName { get; set; }

        public string LastName { get; set; }

        public int CompanyID { get; set; }

        public string JobTitle { get; set; }

        public CrmPerson()
        {
            FirstName = string.Empty;
            LastName = string.Empty;
            CompanyID = 0;
            JobTitle = string.Empty;
        }
    }
}
