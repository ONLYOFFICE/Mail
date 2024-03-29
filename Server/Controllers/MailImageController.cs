﻿using ASC.Web.Api.Routing;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;

namespace ASC.Mail.Controllers
{
    public partial class MailController : ControllerBase
    {
        /// <summary>
        ///    Returns list of all trusted addresses for image displaying.
        /// </summary>
        /// <returns>Addresses list. Email adresses represented as string name@domain.</returns>
        /// <short>Get trusted addresses</short> 
        /// <category>Images</category>
        [HttpGet("display_images/addresses")]
        public IEnumerable<string> GetDisplayImagesAddresses()
        {
            return _displayImagesAddressEngine.Get();
        }

        /// <summary>
        ///    Add the address to trusted addresses.
        /// </summary>
        /// <param name="address">Address for adding. </param>
        /// <returns>Added address</returns>
        /// <short>Add trusted address</short> 
        /// <exception cref="ArgumentException">Exception happens when in parameters is invalid. Text description contains parameter name and text description.</exception>
        /// <category>Images</category>
        [HttpPost("display_images/address")]
        public string AddDisplayImagesAddress(string address)
        {
            _displayImagesAddressEngine.Add(address);

            return address;
        }

        /// <summary>
        ///    Remove the address from trusted addresses.
        /// </summary>
        /// <param name="address">Address for removing</param>
        /// <returns>Removed address</returns>
        /// <short>Remove from trusted addresses</short> 
        /// <exception cref="ArgumentException">Exception happens when in parameters is invalid. Text description contains parameter name and text description.</exception>
        /// <category>Images</category>
        [HttpDelete("display_images/address")]
        public string RemovevDisplayImagesAddress(string address)
        {
            _displayImagesAddressEngine.Remove(address);

            return address;
        }
    }
}
