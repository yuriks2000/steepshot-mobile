﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Steepshot.Core.Models.Common;
using Steepshot.Core.Models.Requests;

namespace Steepshot.Core.Presenters
{
    public class BasePostPresenter : ListPresenter<Post>
    {
        private const int VoteDelay = 3000;
        public static bool IsEnableVote { get; set; }
        
        protected BasePostPresenter()
        {
            IsEnableVote = true;
        }

        public void RemovePostsAt(int index)
        {
            lock (Items)
                Items.RemoveAt(index);
        }

        public int IndexOf(Func<Post, bool> func)
        {
            lock (Items)
            {
                for (var i = 0; i < Items.Count; i++)
                    if (func(Items[i]))
                        return i;
            }
            return -1;
        }


        public async Task<List<string>> TryVote(Post post)
        {
            if (post == null || post.VoteChanging)
                return null;

            post.VoteChanging = true;
            IsEnableVote = false;
            NotifySourceChanged();

            var errors = await TryRunTask(Vote, OnDisposeCts.Token, post);

            post.VoteChanging = false;
            IsEnableVote = true;
            NotifySourceChanged();

            return errors;
        }

        private async Task<List<string>> Vote(CancellationToken ct, Post post)
        {
            var request = new VoteRequest(User.UserInfo, post.Vote ? VoteType.Down : VoteType.Up, post.Url);
            var response = await Api.Vote(request, ct);

            if (response != null && response.Success)
            {
                var td = DateTime.Now - response.Result.VoteTime;
                if (VoteDelay > td.Milliseconds + 300)
                    await Task.Delay(VoteDelay - td.Milliseconds, ct);

                post.Vote = !post.Vote;
                post.Flag = false;
                post.TotalPayoutReward = response.Result.NewTotalPayoutReward;
                post.NetVotes = response.Result.NetVotes;
            }

            return response?.Errors;
        }


        public async Task<List<string>> TryFlag(Post post)
        {
            if (post == null || post.VoteChanging)
                return null;

            post.VoteChanging = true;
            IsEnableVote = false;
            NotifySourceChanged();

            var errors = await TryRunTask(Flag, OnDisposeCts.Token, post);

            post.VoteChanging = false;
            IsEnableVote = true;
            NotifySourceChanged();

            return errors;
        }

        private async Task<List<string>> Flag(CancellationToken ct, Post post)
        {
            var request = new VoteRequest(User.UserInfo, post.Flag ? VoteType.Down : VoteType.Flag, post.Url);
            var response = await Api.Vote(request, ct);
            if (response == null)
                return null;

            if (response.Success)
            {
                post.Flag = !post.Flag;
                post.Vote = false;
                post.TotalPayoutReward = response.Result.NewTotalPayoutReward;
                post.NetVotes = response.Result.NetVotes;
                var td = DateTime.Now - response.Result.VoteTime;
                if (VoteDelay > td.Milliseconds + 300)
                    await Task.Delay(VoteDelay - td.Milliseconds, ct);
            }
            return response.Errors;
        }
    }
}
