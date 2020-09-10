using AuthDemoXForms.Models;
using AuthDemoXForms.Models.Facebook;
using AuthDemoXForms.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace AuthDemoXForms.ViewModels
{
    public class AuthViewModel : BaseViewModel
    {
        public Command GoogleAuthCommand { get; set; }
        const string AuthenticationUrl = "https://testingxamauth.azurewebsites.net/account/mobileauth/";
        public Command FacebookAuthCommand { get; set; }

        public AuthViewModel()
        {
            GoogleAuthCommand = new Command(async () => await OnAuthenticate("Google"));
            FacebookAuthCommand = new Command(async () => await OnAuthenticate("Facebook"));
        }

        async Task OnAuthenticate(string scheme)
        {
            try
            {
                var authUrl = new Uri(AuthenticationUrl + scheme);
                var callbackUrl = new Uri("xamarinapp://");

                var result = await WebAuthenticator.AuthenticateAsync(authUrl, callbackUrl);

                string authToken = result.AccessToken;
                string refreshToken = result.RefreshToken;
                var jwtTokenExpiresIn = result.Properties["jwt_token_expires"];
                //var refreshTokenExpiresIn = result.Properties["refresh_token_expires"];

                //Testing token wrong expiry time
                jwtTokenExpiresIn = TimeSpan.FromSeconds(Convert.ToInt64(jwtTokenExpiresIn)).TotalSeconds.ToString();

                var userInfo = new Dictionary<string, string>
                {
                    { "token", authToken },
                    { "name", $"{result.Properties["firstName"]} {result.Properties["secondName"]}"},
                    { "picture", HttpUtility.UrlDecode(result.Properties["picture"]) }
                };

                var url = "UserProfil" + '?' + string.Join("&", userInfo.Select(kvp => $"{WebUtility.UrlEncode(kvp.Key)}={WebUtility.UrlEncode(kvp.Value)}"));
                await AppShell.Current.GoToAsync(url);
            }
            catch (TaskCanceledException)
            {
                //Note: User exited auth flow;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                throw;
            }
        }
    }
}
