using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AuthDemoWeb.Models;
using AuthDemoWeb.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AuthDemoWeb.Controllers
{
    [Route("Account/[action]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        const string Callback = "xamarinapp";
        private readonly SignInManager<AppUser> _signInManager;
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly FacebookAuthService _facebookAuthService;

        public AccountController(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            IConfiguration configuration,
            FacebookAuthService facebookAuthService
            )
        {
            _facebookAuthService = facebookAuthService;
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
        }

        [HttpGet("{scheme}")]
        public async Task MobileAuth([FromRoute]string scheme)
        {
            //NOTE: see https://docs.microsoft.com/en-us/xamarin/essentials/web-authenticator?tabs=android
            var auth = await Request.HttpContext.AuthenticateAsync(scheme);

            if (!auth.Succeeded
                || auth?.Principal == null
                || !auth.Principal.Identities.Any(id => id.IsAuthenticated)
                || string.IsNullOrEmpty(auth.Properties.GetTokenValue("access_token")))
            {
                // Not authenticated, challenge
                await Request.HttpContext.ChallengeAsync(scheme);
            }
            else
            {
                var claims = auth.Principal.Identities.FirstOrDefault()?.Claims;

                var email = string.Empty;
                email = claims?.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
                var givenName = claims?.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.GivenName)?.Value;
                var surName = claims?.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Surname)?.Value;
                var nameIdentifier = claims?.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                string picture = string.Empty;

                if (scheme == "Facebook")
                {
                    picture = await _facebookAuthService.GetFacebookProfilePicURL(auth.Properties.GetTokenValue("access_token"));
                }
                else
                    picture = claims?.FirstOrDefault(c => c.Type == "picture")?.Value;

                var appUser = new AppUser
                {
                    Email = email,
                    FirstName = givenName,
                    SecondName = surName,
                    PictureURL = picture
                };

                await CreateOrGetUser(appUser);
                var authToken = GenerateJwtToken(appUser);

                // Get parameters to send back to the callback
                var qs = new Dictionary<string, string>
                {
                    { "access_token", authToken.token },
                    { "refresh_token",  string.Empty },
                    { "jwt_token_expires", authToken.expirySeconds.ToString() },
                    { "email", email },
                    { "firstName", givenName },
                    { "picture", picture },
                    { "secondName", surName },
                };

                // Build the result url
                var url = Callback + "://#" + string.Join(
                    "&",
                    qs.Where(kvp => !string.IsNullOrEmpty(kvp.Value) && kvp.Value != "-1")
                    .Select(kvp => $"{WebUtility.UrlEncode(kvp.Key)}={WebUtility.UrlEncode(kvp.Value)}"));

                // Redirect to final url
                Request.HttpContext.Response.Redirect(url);
            }
        }


        [Authorize]
        [HttpGet]
        public IActionResult TestAuth()
        {
            return Ok("Success");
        }

        [Authorize]
        [HttpGet]
        public async Task Signout()
        {
            await Request.HttpContext.SignOutAsync();
            await _signInManager.SignOutAsync();
        }


        private async Task CreateOrGetUser(AppUser appUser)
        {
            var user = await _userManager.FindByEmailAsync(appUser.Email);

            if (user == null)
            {
                //Create a username unique
                appUser.UserName = CreateUniqueUserName($"{appUser.FirstName} {appUser.SecondName}");
                var result = await _userManager.CreateAsync(appUser);
                user = appUser;
            }

            await _signInManager.SignInAsync(user, true);
        }
        string CreateUniqueUserName(string userName)
        {
            var uname = userName.Replace(' ', '-') + "-" + Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            Regex rgx = new Regex("[^a-zA-Z0-9 -]");
            return rgx.Replace(uname, "");
        }

        private (string token, double expirySeconds) GenerateJwtToken(AppUser user)
        {
            var issuedAt = DateTimeOffset.UtcNow;

            //Learn more about JWT claims at: https://tools.ietf.org/html/rfc7519#section-4
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), //Subject, should be unique in this scope
                new Claim(JwtRegisteredClaimNames.Iat, //Issued at, when the token is issued
                    issuedAt.ToUnixTimeMilliseconds().ToString(), ClaimValueTypes.Integer64),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), //Unique identifier for this specific token

                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtKey"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = issuedAt.AddMinutes(Convert.ToDouble(_configuration["JwtExpire"]));
            var expirySeconds = (long)TimeSpan.FromSeconds(Convert.ToDouble(_configuration["JwtExpire"])).TotalSeconds;

            var token = new JwtSecurityToken(
                _configuration["JwtIssuer"],
                _configuration["JwtIssuer"],
                claims,
                expires: expires.DateTime,
                signingCredentials: creds
            );

            return (new JwtSecurityTokenHandler().WriteToken(token), expirySeconds);
        }
    }
}