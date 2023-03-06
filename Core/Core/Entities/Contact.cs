namespace ASC.Mail.Core.Entities;

public sealed class Contact : IEquatable<Contact>
{
    public int Id { get; set; }
    public string User { get; set; }
    public int Tenant { get; set; }
    public string ContactName { get; set; }
    public string Address { get; set; }
    public string Description { get; set; }
    public ContactType Type { get; set; }
    public bool HasPhoto { get; set; }

    public bool Equals(Contact other)
    {
        if (other == null) return false;
        return string.Equals(User, other.User, StringComparison.InvariantCultureIgnoreCase)
               && Tenant == other.Tenant
               && string.Equals(ContactName, other.ContactName, StringComparison.InvariantCultureIgnoreCase)
               && string.Equals(Address, other.Address, StringComparison.InvariantCultureIgnoreCase)
               && string.Equals(Description, other.Description, StringComparison.InvariantCultureIgnoreCase)
               && Type == other.Type
               && HasPhoto == other.HasPhoto;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals(obj as Contact);
    }

    public override int GetHashCode()
    {
        return User.GetHashCode() ^ Tenant.GetHashCode() ^ ContactName.GetHashCode() ^ Address.GetHashCode() ^
               Description.GetHashCode() ^ Type.GetHashCode() ^ HasPhoto.GetHashCode();
    }
}
