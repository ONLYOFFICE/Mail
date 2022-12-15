using System.Text.Json.Serialization;

namespace ASC.Mail.Core.Core.Entities
{
    public class CrmDomainObject
    {
        [JsonPropertyName("id")]
        public virtual int ID { get; set; }

        public override int GetHashCode()
        {
            return (GetType().FullName + "|" + ID.GetHashCode()).GetHashCode();
        }

        public override bool Equals(object obj)
        {
            CrmDomainObject domainObject = obj as CrmDomainObject;
            return domainObject != null && ((!IsTransient() && !domainObject.IsTransient() && ID.Equals(domainObject.ID)) || ((IsTransient() || domainObject.IsTransient()) && GetHashCode().Equals(domainObject.GetHashCode())));
        }

        private bool IsTransient()
        {
            return ID.Equals(0);
        }
    }
}
