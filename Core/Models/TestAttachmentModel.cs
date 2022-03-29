namespace ASC.Mail.Models;

public class TestAttachmentModel
{
    public string Filename { get; set; }
    public Stream Stream { get; set; }
    public string ContentType { get; set; }
}
