using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;

namespace ASC.Mail.Core.Utils
{
    public class DnsLookup
    {
        private readonly IDnsResolver _sDnsResolver;

        private readonly DnsClient _dnsClient;

        public DnsLookup()
        {
            _dnsClient = DnsClient.Default;
            _sDnsResolver = new DnsStubResolver(_dnsClient);
        }

        public List<MxRecord> GetDomainMxRecords(string domainName)
        {
            ArgumentNullOrEmptyException.ThrowIfNullOrEmpty(domainName, "domainName");
            return DnsResolve<MxRecord>(domainName, RecordType.Mx);
        }

        public bool IsDomainMxRecordExists(string domainName, string mxRecord)
        {
            ArgumentNullOrEmptyException.ThrowIfNullOrEmpty(domainName, "domainName");
            ArgumentNullOrEmptyException.ThrowIfNullOrEmpty(mxRecord, "mxRecord");
            DomainName mxDomain = DomainName.Parse(mxRecord);
            List<MxRecord> domainMxRecords = GetDomainMxRecords(domainName);
            return domainMxRecords.Any((MxRecord mx) => mx.ExchangeDomainName.Equals(mxDomain));
        }

        public bool IsDomainExists(string domainName)
        {
            ArgumentNullOrEmptyException.ThrowIfNullOrEmpty(domainName, "domainName");
            DnsMessage dnsMessage = GetDnsMessage(domainName);
            return dnsMessage.AnswerRecords.Count != 0;
        }

        public List<ARecord> GetDomainARecords(string domainName)
        {
            ArgumentNullOrEmptyException.ThrowIfNullOrEmpty(domainName, "domainName");
            return DnsResolve<ARecord>(domainName, RecordType.A);
        }

        public List<IPAddress> GetDomainIPs(string domainName)
        {
            ArgumentNullOrEmptyException.ThrowIfNullOrEmpty(domainName, "domainName");
            return _sDnsResolver.ResolveHost(domainName);
        }

        public List<TxtRecord> GetDomainTxtRecords(string domainName)
        {
            ArgumentNullOrEmptyException.ThrowIfNullOrEmpty(domainName, "domainName");
            return DnsResolve<TxtRecord>(domainName, RecordType.Txt);
        }

        public bool IsDomainTxtRecordExists(string domainName, string recordValue)
        {
            List<TxtRecord> domainTxtRecords = GetDomainTxtRecords(domainName);
            return domainTxtRecords.Any((TxtRecord txtRecord) => txtRecord.TextData.Trim('"').Equals(recordValue, StringComparison.InvariantCultureIgnoreCase));
        }

        public bool IsDomainDkimRecordExists(string domainName, string dkimSelector, string dkimValue)
        {
            string domainName2 = dkimSelector + "._domainkey." + domainName;
            List<TxtRecord> domainTxtRecords = GetDomainTxtRecords(domainName2);
            return domainTxtRecords.Any((TxtRecord txtRecord) => txtRecord.TextData.Trim('"').Equals(dkimValue));
        }

        public bool IsDomainPtrRecordExists(IPAddress ipAddress, string domainName)
        {
            ArgumentNullOrEmptyException.ThrowIfNullOrEmpty(domainName, "domainName");
            ArgumentNullException.ThrowIfNull(ipAddress, "ipAddress");
            DomainName other = DomainName.Parse(domainName);
            DomainName domainName2 = _sDnsResolver.ResolvePtr(ipAddress);
            return domainName2.Equals(other);
        }

        public bool IsDomainPtrRecordExists(string ipAddress, string domainName)
        {
            return IsDomainPtrRecordExists(IPAddress.Parse(ipAddress), domainName);
        }

        private DnsMessage GetDnsMessage(string domainName, RecordType? type = null)
        {
            ArgumentNullOrEmptyException.ThrowIfNullOrEmpty(domainName, "domainName");
            DomainName name = DomainName.Parse(domainName);
            DnsMessage dnsMessage = (type.HasValue ? _dnsClient.Resolve(name, type.Value) : _dnsClient.Resolve(name));
            if (dnsMessage == null || (dnsMessage.ReturnCode != 0 && dnsMessage.ReturnCode != ReturnCode.NxDomain))
            {
                throw new SystemException();
            }

            return dnsMessage;
        }

        private List<T> DnsResolve<T>(string domainName, RecordType type)
        {
            ArgumentNullOrEmptyException.ThrowIfNullOrEmpty(domainName, "domainName");
            DnsMessage dnsMessage = GetDnsMessage(domainName, type);
            return dnsMessage.AnswerRecords.Where((DnsRecordBase r) => r.RecordType == type).Cast<T>().ToList();
        }
    }
}
