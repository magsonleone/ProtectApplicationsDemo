using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace ProtectApplication.Cookies.Controllers
{
    public class RestrictedController : Controller
    {
        private readonly IDataProtectionProvider _protector;
        private readonly IKeyManager _keyManager;

        public RestrictedController(
            IDataProtectionProvider protector,
            IKeyManager keyManager)
        {
            _protector = protector;
            _keyManager = keyManager;
        }
        [Authorize]
        public IActionResult Index()
        {
            return View();
        }

        [Authorize(Roles = "Managers")]
        public IActionResult RoleBased()
        {
            return View();
        }

        [Authorize(Policy = "ManagerOnly")]
        public IActionResult ClaimBased()
        {
            return View();
        }

        [Authorize(Policy = "ManagerFromSalesDepartment")]
        public IActionResult PolicyBased()
        {
            return View();
        }

        [Authorize]
        public IActionResult DecryptCookie()
        {
            var cookieManager = new ChunkingCookieManager();
            var cookie = cookieManager.GetRequestCookie(HttpContext, ".AspNetCore.Identity.Application");

            var dataProtector = _protector.CreateProtector("Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationMiddleware", "Identity.Application", "v2");

            //Get the decrypted cookie as plain text
            UTF8Encoding specialUtf8Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            var protectedBytes = Base64UrlTextEncoder.Decode(cookie);

            var plainBytes = dataProtector.Unprotect(protectedBytes);
            var plainText = specialUtf8Encoding.GetString(plainBytes);


            //Get teh decrypted cookies as a Authentication Ticket
            var ticketDataFormat = new TicketDataFormat(dataProtector);
            var ticket = ticketDataFormat.Unprotect(cookie);

            return View(new CookieDetails(plainText, ticket));
        }

        [Authorize]
        public IActionResult GetJwks()
        {
            return View(GetKeys());
        }

        /// <summary>
        /// Kind of jwks
        /// </summary>
        /// <returns></returns>
        private IEnumerable<JsonWebKey> GetKeys()
        {
            var jwks = new List<JsonWebKey>();
            // Get keys from DataProtectionKeys
            var storedKey = _keyManager.GetAllKeys();
            foreach (var key in storedKey.OrderByDescending(b => b.CreationDate))
            {
                var defaultKeyId = key.KeyId;

                // Export for Xml to get access to masterkey
                var descriptorXmlInfo = key.Descriptor.ExportToXml();
                var keyData = new XElement("key", descriptorXmlInfo.SerializedDescriptorElement.LastNode).Value;

                var bKey = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetBytes(keyData);
                var symetricKey = new HMACSHA256(bKey);

                var jwk = JsonWebKeyConverter.ConvertFromSymmetricSecurityKey(new SymmetricSecurityKey(symetricKey.Key));
                jwk.KeyId = Base64UrlEncoder.Encode(defaultKeyId.ToString());

                jwks.Add(jwk);
            }

            return jwks;
        }
    }

    public class CookieDetails
    {
        public string PlainText { get; }
        public AuthenticationTicket Ticket { get; }

        public CookieDetails(string plainText, AuthenticationTicket ticket)
        {
            PlainText = plainText;
            Ticket = ticket;
        }
    }
}
