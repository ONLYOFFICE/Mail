using ASC.Mail.Models;

using Microsoft.AspNetCore.Mvc;

namespace ASC.Mail.Controllers
{
    public partial class MailController : ControllerBase
    {
        /// <summary>
        /// Create sample message [Tests]
        /// </summary>
        /// <param name="model">instance of TestMessageModel</param>
        /// <returns>Id message</returns>
        /// <category>Tests</category>
        /// <visible>false</visible>
        [HttpPost(@"messages/sample/create")]
        public int CreateSampleMessage(TestMessageModel model)
        {
            var id = _testEngine
                .CreateSampleMessage(model, add2Index: true);

            return id;
        }

        /// <summary>
        /// Append attachment to sample message [Tests]
        /// </summary>
        /// <param name="messageId">Id of any message</param>
        /// <param name="filename">File name</param>
        /// <param name="stream">File stream</param>
        /// <param name="contentType">File content type</param>
        /// <returns>Id message</returns>
        /// <category>Tests</category>
        /// <visible>false</visible>
        [HttpPost(@"messages/sample/attachments/append")]
        public MailAttachmentData AppendAttachmentsToSampleMessage(int? messageId, TestAttachmentModel model)
        {
            var data = _testEngine
                .AppendAttachmentsToSampleMessage(messageId, model);

            return data;
        }

        /// <summary>
        /// Load sample message from EML [Tests]
        /// </summary>
        /// <param name="model">instance of TestMessageModel</param>
        /// <returns>Id message</returns>
        /// <category>Tests</category>
        /// <visible>false</visible>
        [HttpPost(@"messages/sample/eml/load")]
        public int LoadSampleMessage(TestMessageModel model)
        {
            var id = _testEngine
                .LoadSampleMessage(model, true);

            return id;
        }
    }
}
