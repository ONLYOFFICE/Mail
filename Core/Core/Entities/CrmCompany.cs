namespace ASC.Mail.Core.Core.Entities
{
    [Serializable]
    public class CrmCompany : ASC.Core.Common.EF.Model.CrmContact
    {
        public CrmCompany()
        {
            CompanyName = string.Empty;
        }
    }
}
