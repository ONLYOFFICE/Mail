using ASC.Mail.Models;

using Microsoft.AspNetCore.Mvc;

using System;
using System.Collections.Generic;

namespace ASC.Mail.Controllers
{
    public partial class MailController : ControllerBase
    {
        /// <summary>
        ///    Returns the list of alerts for the authenticated user
        /// </summary>
        /// <returns>Alerts list</returns>
        /// <short>Get alerts list</short> 
        /// <category>Alerts</category>
        [HttpGet("alert")]
        public IList<MailAlertData> GetAlerts()
        {
            var alerts = _alertEngine.GetAlerts();
            return alerts;
        }

        /// <summary>
        ///    Deletes the alert with the ID specified in the request
        /// </summary>
        /// <param name="id">Alert ID</param>
        /// <returns>Deleted alert id. Same as request parameter.</returns>
        /// <short>Delete alert by ID</short> 
        /// <category>Alerts</category>
        [HttpDelete("alert/{id}")]
        public long DeleteAlert(long id)
        {
            if (id < 0)
                throw new ArgumentException(@"Invalid alert id. Id must be positive integer.", "id");

            var success = _alertEngine.DeleteAlert(id);

            if (!success)
                throw new Exception("Delete failed");

            return id;
        }
    }
}
