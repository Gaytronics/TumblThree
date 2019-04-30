﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using TumblThree.Applications.Extensions;

namespace TumblThree.Applications.Services
{
    [Export, Export(typeof(ILoginService))]
    internal class LoginService : ILoginService
    {
        private readonly IShellService shellService;
        private readonly ISharedCookieService cookieService;
        private readonly IWebRequestFactory webRequestFactory;
        private string tumblrKey = string.Empty;
        private bool tfaNeeded = false;
        private string tumblrTFAKey = string.Empty;

        [ImportingConstructor]
        public LoginService(IShellService shellService, IWebRequestFactory webRequestFactory, ISharedCookieService cookieService)
        {
            this.shellService = shellService;
            this.webRequestFactory = webRequestFactory;
            this.cookieService = cookieService;
        }

        public async Task PerformTumblrLoginAsync(string login, string password)
        {
            try
            {
                string document = await RequestTumblrKey();
                tumblrKey = ExtractTumblrKey(document);
                await Register(login, password);
                document = await Authenticate(login, password);
                if (tfaNeeded)
                    tumblrTFAKey = ExtractTumblrTFAKey(document);
            }
            catch (TimeoutException)
            {
            }
        }

        public void PerformTumblrLogout()
        {
            HttpWebRequest request = webRequestFactory.CreateGetReqeust("https://www.tumblr.com/");
            cookieService.GetUriCookie(request.CookieContainer, new Uri("https://www.tumblr.com/"));
            cookieService.RemoveUriCookie(new Uri("https://www.tumblr.com"));
            Cookie tosCookie =
                request.CookieContainer.GetCookies(
                    new Uri("https://www.tumblr.com/"))["pfg"]; // pfg cookie contains ToS/GDPR agreement
            var tosCookieCollection = new CookieCollection
            {
                tosCookie
            };
            cookieService.SetUriCookie(tosCookieCollection);
        }

        public bool CheckIfTumblrTFANeeded() => tfaNeeded;

        public async Task PerformTumblrTFALoginAsync(string login, string tumblrTFAAuthCode)
        {
            try
            {
                await SubmitTFAAuthCode(login, tumblrTFAAuthCode);
            }
            catch (TimeoutException)
            {
            }
        }

        private static string ExtractTumblrKey(string document) => Regex.Match(document, "id=\"tumblr_form_key\" content=\"([\\S]*)\">").Groups[1].Value;

        private async Task<string> RequestTumblrKey()
        {
            var url = "https://www.tumblr.com/login";
            HttpWebRequest request = webRequestFactory.CreateGetReqeust(url);
            cookieService.GetUriCookie(request.CookieContainer, new Uri("https://www.tumblr.com/"));
            using (var response = await request.GetResponseAsync().TimeoutAfter(shellService.Settings.TimeOut) as HttpWebResponse)
            {
                cookieService.SetUriCookie(response.Cookies);
                using (Stream stream = webRequestFactory.GetStreamForApiRequest(response.GetResponseStream()))
                {
                    using (var buffer = new BufferedStream(stream))
                    {
                        using (var reader = new StreamReader(buffer))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
            }
        }

        private async Task Register(string login, string password)
        {
            var url = "https://www.tumblr.com/svc/account/register";
            var referer = "https://www.tumblr.com/login";
            var headers = new Dictionary<string, string>();
            HttpWebRequest request = webRequestFactory.CreatePostXhrReqeust(url, referer, headers);
            cookieService.GetUriCookie(request.CookieContainer, new Uri("https://www.tumblr.com/"));
            var parameters = new Dictionary<string, string>
            {
                { "determine_email", login },
                { "user[email]", "" },
                { "user[password]", "" },
                { "tumblelog[name]", "" },
                { "user[age]", "" },
                { "context", "no_referer" },
                { "version", "STANDARD" },
                { "follow", "" },
                { "form_key", tumblrKey },
                { "seen_suggestion", "0" },
                { "used_suggestion", "0" },
                { "used_auto_suggestion", "0" },
                { "about_tumblr_slide", "" },
                { "tracking_url", "/login" },
                { "tracking_version", "modal" },
                {
                    "random_username_suggestions",
                    "[\"KawaiiBouquetStranger\",\"KeenTravelerFury\",\"RainyMakerTastemaker\",\"SuperbEnthusiastCollective\",\"TeenageYouthFestival\"]"
                },
                { "action", "signup_determine" },
            };
            await webRequestFactory.PerformPostReqeustAsync(request, parameters);
            using (var response = await request.GetResponseAsync().TimeoutAfter(shellService.Settings.TimeOut) as HttpWebResponse)
            {
                cookieService.SetUriCookie(response.Cookies);
            }
        }

        private async Task<string> Authenticate(string login, string password)
        {
            var url = "https://www.tumblr.com/login";
            var referer = "https://www.tumblr.com/login";
            var headers = new Dictionary<string, string>();
            HttpWebRequest request = webRequestFactory.CreatePostReqeust(url, referer, headers);
            cookieService.GetUriCookie(request.CookieContainer, new Uri("https://www.tumblr.com/"));
            var parameters = new Dictionary<string, string>
            {
                { "determine_email", login },
                { "user[email]", login },
                { "user[password]", password },
                { "tumblelog[name]", "" },
                { "user[age]", "" },
                { "context", "no_referer" },
                { "version", "STANDARD" },
                { "follow", "" },
                { "form_key", tumblrKey },
                { "seen_suggestion", "0" },
                { "used_suggestion", "0" },
                { "used_auto_suggestion", "0" },
                { "about_tumblr_slide", "" },
                {
                    "random_username_suggestions",
                    "[\"KawaiiBouquetStranger\",\"KeenTravelerFury\",\"RainyMakerTastemaker\",\"SuperbEnthusiastCollective\",\"TeenageYouthFestival\"]"
                },
                { "action", "signup_determine" }
            };
            await webRequestFactory.PerformPostReqeustAsync(request, parameters);
            using (var response = await request.GetResponseAsync().TimeoutAfter(shellService.Settings.TimeOut) as HttpWebResponse)
            {
                if (request.Address == new Uri("https://www.tumblr.com/login")) // TFA required
                {
                    tfaNeeded = true;
                    cookieService.SetUriCookie(response.Cookies);
                    using (var stream = webRequestFactory.GetStreamForApiRequest(response.GetResponseStream()))
                    {
                        using (var buffer = new BufferedStream(stream))
                        {
                            using (var reader = new StreamReader(buffer))
                            {
                                return reader.ReadToEnd();
                            }
                        }
                    }
                }

                cookieService.SetUriCookie(request.CookieContainer.GetCookies(new Uri("https://www.tumblr.com/")));
                return string.Empty;
            }
        }

        private static string ExtractTumblrTFAKey(string document) => Regex.Match(document, "name=\"tfa_form_key\" value=\"([\\S]*)\"/>").Groups[1].Value;

        private async Task SubmitTFAAuthCode(string login, string tumblrTFAAuthCode)
        {
            var url = "https://www.tumblr.com/login";
            var referer = "https://www.tumblr.com/login";
            var headers = new Dictionary<string, string>();
            HttpWebRequest request = webRequestFactory.CreatePostReqeust(url, referer, headers);
            cookieService.GetUriCookie(request.CookieContainer, new Uri("https://www.tumblr.com/"));
            var parameters = new Dictionary<string, string>
            {
                { "determine_email", login },
                { "user[email]", login },
                { "tumblelog[name]", "" },
                { "user[age]", "" },
                { "context", "login" },
                { "version", "STANDARD" },
                { "follow", "" },
                { "form_key", tumblrKey },
                { "tfa_form_key", tumblrTFAKey },
                { "tfa_response_field", tumblrTFAAuthCode },
                { "http_referer", "https://www.tumblr.com/login" },
                { "seen_suggestion", "0" },
                { "used_suggestion", "0" },
                { "used_auto_suggestion", "0" },
                { "about_tumblr_slide", "" },
                {
                    "random_username_suggestions",
                    "[\"KawaiiBouquetStranger\",\"KeenTravelerFury\",\"RainyMakerTastemaker\",\"SuperbEnthusiastCollective\",\"TeenageYouthFestival\"]"
                },
                { "action", "signup_determine" }
            };
            await webRequestFactory.PerformPostReqeustAsync(request, parameters);
            using (var response = await request.GetResponseAsync().TimeoutAfter(shellService.Settings.TimeOut) as HttpWebResponse)
            {
                cookieService.SetUriCookie(request.CookieContainer.GetCookies(new Uri("https://www.tumblr.com/")));
            }
        }

        public bool CheckIfLoggedInAsync()
        {
            HttpWebRequest request = webRequestFactory.CreateGetReqeust("https://www.tumblr.com/");
            cookieService.GetUriCookie(request.CookieContainer, new Uri("https://www.tumblr.com/"));
            return request.CookieContainer.GetCookieHeader(new Uri("https://www.tumblr.com/")).Contains("pfs");
        }

        public async Task<string> GetTumblrUsernameAsync()
        {
            var tumblrAccountSettingsUrl = "https://www.tumblr.com/settings/account";
            HttpWebRequest request = webRequestFactory.CreateGetReqeust(tumblrAccountSettingsUrl);
            cookieService.GetUriCookie(request.CookieContainer, new Uri("https://www.tumblr.com/"));
            string document = await webRequestFactory.ReadReqestToEndAsync(request);
            return ExtractTumblrUsername(document);
        }

        private static string ExtractTumblrUsername(string document) => Regex.Match(document, "<p class=\"accordion_label accordion_trigger\">([\\S]*)</p>").Groups[1].Value;
    }
}
