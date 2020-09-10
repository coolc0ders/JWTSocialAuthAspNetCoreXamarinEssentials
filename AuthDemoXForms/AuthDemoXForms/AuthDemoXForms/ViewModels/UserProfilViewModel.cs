using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Input;
using Xamarin.Forms;

namespace AuthDemoXForms.ViewModels
{
    [QueryProperty("Token", "token")]
    [QueryProperty("UserPic", "picture")]
    [QueryProperty("UserName", "name")]
    public class UserProfilViewModel : BaseViewModel
    {
        private string _userPic;
        public string UserPic
        {
            get { return _userPic; }
            set {
                var img = HttpUtility.UrlDecode(value);
                img = HttpUtility.UrlDecode(value);
                SetProperty(ref _userPic, img); }
        }

        private string _userName;
        public string UserName
        {
            get { return _userName; }
            set { 
                SetProperty(ref _userName, HttpUtility.UrlDecode(value)); }
        }

        private string _token;
        public string Token
        {
            get { return _token; }
            set { 
                SetProperty(ref _token, value); }
        }
        private string _weatherData;

        public string WeatherData
        {
            get { return _weatherData; }
            set { 
                SetProperty(ref _weatherData, value); }
        }

        public ICommand TestAuthCommand { get; set; }

        public UserProfilViewModel()
        {
            TestAuthCommand = new Command(async () => await GetWeatherData());
        }

        private async Task GetWeatherData()
        {
            IsBusy = true;

            using var httppClient = new HttpClient();
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri("https://testingxamauth.azurewebsites.net/WeatherForecast"),
                Headers =
                {
                    { "Authorization", $"Bearer {Token}" }
                }
            };
            var response = await httppClient.SendAsync(request);

            string respStr = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                WeatherData = respStr;
            }
            else
                WeatherData = $"Error while sending the request: {respStr}";

            IsBusy = false;
        }
    }
}
