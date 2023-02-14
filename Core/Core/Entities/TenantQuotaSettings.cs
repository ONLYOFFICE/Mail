namespace ASC.Mail.Core.Core.Entities
{
    [Serializable]
    [DataContract]
    public class TenantQuotaSettings : ISettings<TenantQuotaSettings>
    {
        [DataMember]
        public bool DisableQuota { get; set; }

        public TenantQuotaSettings GetDefault()
        {
            return new TenantQuotaSettings
            {
                DisableQuota = false
            };
        }

        public Guid ID
        {
            get { return new Guid("{62609D95-35D3-4F14-A6BA-2118979E04EA}"); }
        }
    }
}
