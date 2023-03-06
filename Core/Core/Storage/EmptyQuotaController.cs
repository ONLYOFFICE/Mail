namespace ASC.Mail.Core.Core.Storage
{
    public class EmptyQuotaController : IQuotaController
    {
        public EmptyQuotaController()
        {

        }
        public void QuotaUsedAdd(string module, string domain, string dataTag, long size, bool quotaCheckFileSize = true)
        {
            
        }

        public void QuotaUsedCheck(long size)
        {
            
        }

        public void QuotaUsedDelete(string module, string domain, string dataTag, long size)
        {
           
        }

        public void QuotaUsedSet(string module, string domain, string dataTag, long size)
        {
            
        }
    }
}
