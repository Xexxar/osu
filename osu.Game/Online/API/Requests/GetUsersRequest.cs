// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;

namespace osu.Game.Online.API.Requests
{
    public class GetUsersRequest : APIRequest<GetUsersResponse>
    {
        private readonly int[] userIds;

        private const int max_ids_per_request = 50;

        public GetUsersRequest(int[] userIds)
        {
            if (userIds.Length > max_ids_per_request)
                throw new ArgumentException($"{nameof(GetUsersRequest)} calls only support up to {max_ids_per_request} IDs at once");

            this.userIds = userIds;
        }

        protected override string Target => $@"users/?{userIds.Select(u => $"ids[]={u}&").Aggregate((a, b) => a + b)}";
    }
}
