﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers
 * for more information concerning the license and the contributors participating to this project.
 */

using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using AspNet.Security.OAuth.Extensions;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Http.Authentication;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace AspNet.Security.OAuth.Weibo
{
    public class WeiboAuthenticationHandler : OAuthHandler<WeiboAuthenticationOptions>
    {
        public WeiboAuthenticationHandler([NotNull] HttpClient client)
            : base(client)
        {
        }

        protected override async Task<AuthenticationTicket> CreateTicketAsync([NotNull] ClaimsIdentity identity,
            [NotNull] AuthenticationProperties properties, [NotNull] OAuthTokenResponse tokens)
        {
            var address = QueryHelpers.AddQueryString(Options.UserInformationEndpoint, new Dictionary<string, string>
            {
                ["access_token"] = tokens.AccessToken,
                ["uid"] = tokens.Response.Value<string>("uid")
            });

            var request = new HttpRequestMessage(HttpMethod.Get, address);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await Backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, Context.RequestAborted);
            if (!response.IsSuccessStatusCode)
            {
                Logger.LogError("An error occurred while retrieving the user profile: the remote server " +
                                "returned a {Status} response with the following payload: {Headers} {Body}.",
                                /* Status: */ response.StatusCode,
                                /* Headers: */ response.Headers.ToString(),
                                /* Body: */ await response.Content.ReadAsStringAsync());

                throw new HttpRequestException("An error occurred while retrieving the user profile.");
            }

            var payload = JObject.Parse(await response.Content.ReadAsStringAsync());

            identity.AddOptionalClaim(ClaimTypes.NameIdentifier, WeiboAuthenticationHelper.GetId(payload), Options.ClaimsIssuer)
                    .AddOptionalClaim(ClaimTypes.Name, WeiboAuthenticationHelper.GetName(payload), Options.ClaimsIssuer)
                    .AddOptionalClaim(ClaimTypes.Gender, WeiboAuthenticationHelper.GetGender(payload), Options.ClaimsIssuer)
                    .AddOptionalClaim("urn:weibo:screen_name", WeiboAuthenticationHelper.GetScreenName(payload), Options.ClaimsIssuer)
                    .AddOptionalClaim("urn:weibo:profile_image_url", WeiboAuthenticationHelper.GetProfileImageUrl(payload), Options.ClaimsIssuer)
                    .AddOptionalClaim("urn:weibo:avatar_large", WeiboAuthenticationHelper.GetAvatarLarge(payload), Options.ClaimsIssuer)
                    .AddOptionalClaim("urn:weibo:avatar_hd", WeiboAuthenticationHelper.GetAvatarHD(payload), Options.ClaimsIssuer)
                    .AddOptionalClaim("urn:weibo:cover_image_phone", WeiboAuthenticationHelper.GetCoverImagePhone(payload), Options.ClaimsIssuer)
                    .AddOptionalClaim("urn:weibo:location", WeiboAuthenticationHelper.GetLocation(payload), Options.ClaimsIssuer);            

            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, properties, Options.AuthenticationScheme);

            var context = new OAuthCreatingTicketContext(ticket, Context, Options, Backchannel, tokens);
            await Options.Events.CreatingTicket(context);

            return context.Ticket;
        }

        protected override string FormatScope() => string.Join(",", Options.Scope);
    }
}
