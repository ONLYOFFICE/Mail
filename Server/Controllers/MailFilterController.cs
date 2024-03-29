﻿using ASC.Mail.Core.Engine.Operations.Base;
using ASC.Mail.Models;
using ASC.Web.Mail.Resources;

using Microsoft.AspNetCore.Mvc;

using System;
using System.Collections.Generic;
using System.Threading;

namespace ASC.Mail.Controllers
{
    public partial class MailController : ControllerBase
    {
        /// <summary>
        ///    Returns list of the tags used in Mail
        /// </summary>
        /// <returns>Filters list. Filters represented as JSON.</returns>
        /// <short>Get filters list</short> 
        /// <category>Filters</category>
        [HttpGet(@"filters")]
        public IEnumerable<MailSieveFilterData> GetFilters()
        {
            var filters = _filterEngine.GetList();
            return filters;
        }

        /// <summary>
        ///    Creates a new filter
        /// </summary>
        /// <param name="filter"></param>
        /// <returns>Filter</returns>
        /// <short>Create filter</short> 
        /// <category>Filters</category>
        /// <exception cref="ArgumentException">Exception happens when in parameters is invalid. Text description contains parameter name and text description.</exception>
        [HttpPost(@"filters")]
        public MailSieveFilterData CreateFilter(MailSieveFilterData filter)
        {
            var id = _filterEngine.Create(filter);
            filter.Id = id;
            return filter;
        }

        /// <summary>
        ///    Updates the selected filter
        /// </summary>
        /// <param name="filter"></param>
        /// <returns>Updated filter</returns>
        /// <short>Update filter</short> 
        /// <category>Filters</category>
        /// <exception cref="ArgumentException">Exception happens when in parameters is invalid. Text description contains parameter name and text description.</exception>
        [HttpPut(@"filters")]
        public MailSieveFilterData UpdateFilter(MailSieveFilterData filter)
        {
            _filterEngine.Update(filter);

            return filter;
        }

        /// <summary>
        ///    Deletes the selected filter
        /// </summary>
        /// <param name="id">Filter id</param>
        /// <returns>Deleted Filter id</returns>
        /// <short>Delete filter</short> 
        /// <category>Filters</category>
        /// <exception cref="ArgumentException">Exception happens when in parameters is invalid. Text description contains parameter name and text description.</exception>
        [HttpDelete(@"filters/{id}")]
        public int DeleteFilter(int id)
        {
            _filterEngine.Delete(id);

            return id;
        }

        /// <summary>
        ///    Check filter result
        /// </summary>
        /// <param name="filter"></param>
        /// <param optional="true" name="page">Page number</param>
        /// <param optional="true" name="pageSize">Number of messages on page</param>
        /// <returns>List messages</returns>
        /// <short>Check filter</short> 
        /// <category>Filters</category>
        /// <exception cref="ArgumentException">Exception happens when in parameters is invalid. Text description contains parameter name and text description.</exception>
        [HttpGet(@"filters/check")]
        public List<MailMessageData> CheckFilter(MailSieveFilterData filter, int? page, int? pageSize)
        {
            if (!page.HasValue)
                page = 0;

            if (!pageSize.HasValue)
                pageSize = 10;

            var messages = _messageEngine.GetFilteredMessages(filter, page.Value, pageSize.Value, out long total);

            _apiContext.SetTotalCount(total);

            return messages;
        }

        /// <summary>
        ///    Apply filter to existing messages
        /// </summary>
        /// <param name="id">Filter id</param>
        /// <returns>MailOperationResult object</returns>
        /// <short>Check filter</short> 
        /// <category>Filters</category>
        /// <exception cref="ArgumentException">Exception happens when in parameters is invalid. Text description contains parameter name and text description.</exception>
        [HttpGet(@"filters/{id}/apply")]
        public MailOperationStatus ApplyFilter(int id)
        {
            Thread.CurrentThread.CurrentCulture = CurrentCulture;
            Thread.CurrentThread.CurrentUICulture = CurrentCulture;

            try
            {
                return _operationEngine.ApplyFilter(id, TranslateMailOperationStatus);
            }
            catch (Exception)
            {
                throw new Exception(MailApiErrorsResource.ErrorInternalServer);
            }
        }
    }
}
