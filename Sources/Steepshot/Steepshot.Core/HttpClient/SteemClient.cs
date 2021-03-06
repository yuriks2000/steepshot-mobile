﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ditch.Core;
using Ditch.Steem.Old.Models.Operations;
using Steepshot.Core.Models.Common;
using Steepshot.Core.Models.Requests;
using Steepshot.Core.Serializing;   
using DitchFollowType = Ditch.Steem.Old.Models.Enums.FollowType;
using DitchBeneficiary = Ditch.Steem.Old.Models.Operations.Beneficiary;
using Ditch.Core.JsonRpc;
using Steepshot.Core.Errors;
using Steepshot.Core.Models.Enums;
using Steepshot.Core.Localization;
using Steepshot.Core.Utils;
using Cryptography.ECDSA;
using Ditch.Steem.Old;
using Ditch.Steem.Old.Models.Objects;
using Ditch.Steem.Old.Models.Other;

namespace Steepshot.Core.HttpClient
{
    internal class SteemClient : BaseDitchClient
    {
        private readonly OperationManager _operationManager;


        public override bool IsConnected => _operationManager.IsConnected;

        public override KnownChains Chain => KnownChains.Steem;

        public SteemClient(JsonNetConverter jsonConverter) : base(jsonConverter)
        {
            _operationManager = new OperationManager();
        }

        public override bool TryReconnectChain(CancellationToken token)
        {
            if (EnableWrite)
                return EnableWrite;

            var lockWasTaken = false;
            try
            {
                Monitor.Enter(SyncConnection, ref lockWasTaken);
                if (!EnableWrite)
                {
                    var cUrls = AppSettings.ConfigManager.SteemNodeConfigs
                        .Where(n => n.IsEnabled)
                        .OrderBy(n => n.Order)
                        .Select(n => n.Url)
                        .ToArray();
                    if (cUrls.Any() && _operationManager.TryConnectTo(cUrls, token))
                        EnableWrite = true;
                }
            }
            catch (Exception)
            {
                //todo nothing
            }
            finally
            {
                if (lockWasTaken)
                    Monitor.Exit(SyncConnection);
            }
            return EnableWrite;
        }

        #region Post requests

        public override async Task<OperationResult<VoidResponse>> Vote(VoteModel model, CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                if (!TryReconnectChain(ct))
                    return new OperationResult<VoidResponse>(new AppError(LocalizationKeys.EnableConnectToBlockchain));

                var keys = ToKeyArr(model.PostingKey);
                if (keys == null)
                    return new OperationResult<VoidResponse>(new AppError(LocalizationKeys.WrongPrivatePostingKey));

                short weigth = 0;
                if (model.Type == VoteType.Up)
                    weigth = (short)(AppSettings.User.VotePower * 100);
                if (model.Type == VoteType.Flag)
                    weigth = -10000;

                var op = new VoteOperation(model.Login, model.Author, model.Permlink, weigth);
                var resp = _operationManager.BroadcastOperationsSynchronous(keys, ct, op);

                var result = new OperationResult<VoidResponse>();
                if (!resp.IsError)
                    result.Result = new VoidResponse();
                else
                    OnError(resp, result);

                return result;
            }, ct);
        }

        public override async Task<OperationResult<VoidResponse>> Follow(FollowModel model, CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                if (!TryReconnectChain(ct))
                    return new OperationResult<VoidResponse>(new AppError(LocalizationKeys.EnableConnectToBlockchain));

                var keys = ToKeyArr(model.PostingKey);
                if (keys == null)
                    return new OperationResult<VoidResponse>(new AppError(LocalizationKeys.WrongPrivatePostingKey));

                var op = model.Type == FollowType.Follow
                    ? new FollowOperation(model.Login, model.Username, DitchFollowType.Blog, model.Login)
                    : new UnfollowOperation(model.Login, model.Username, model.Login);
                var resp = _operationManager.BroadcastOperationsSynchronous(keys, ct, op);

                var result = new OperationResult<VoidResponse>();

                if (!resp.IsError)
                    result.Result = new VoidResponse();
                else
                    OnError(resp, result);

                return result;
            }, ct);
        }

        public override async Task<OperationResult<VoidResponse>> LoginWithPostingKey(AuthorizedModel model, CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                if (!TryReconnectChain(ct))
                    return new OperationResult<VoidResponse>(new AppError(LocalizationKeys.EnableConnectToBlockchain));

                var keys = ToKeyArr(model.PostingKey);
                if (keys == null)
                    return new OperationResult<VoidResponse>(new AppError(LocalizationKeys.WrongPrivatePostingKey));

                var op = new FollowOperation(model.Login, "steepshot", DitchFollowType.Blog, model.Login);
                var resp = _operationManager.VerifyAuthority(keys, ct, op);

                var result = new OperationResult<VoidResponse>();

                if (!resp.IsError)
                    result.Result = new VoidResponse();
                else
                    OnError(resp, result);

                return result;
            }, ct);
        }

        public override async Task<OperationResult<VoidResponse>> CreateOrEdit(CommentModel model, CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                if (!TryReconnectChain(ct))
                    return new OperationResult<VoidResponse>(new AppError(LocalizationKeys.EnableConnectToBlockchain));

                var keys = ToKeyArr(model.PostingKey);
                if (keys == null)
                    return new OperationResult<VoidResponse>(new AppError(LocalizationKeys.WrongPrivatePostingKey));

                var op = new CommentOperation(model.ParentAuthor, model.ParentPermlink, model.Author, model.Permlink, model.Title, model.Body, model.JsonMetadata);

                BaseOperation[] ops;
                if (model.Beneficiaries != null && model.Beneficiaries.Any())
                {
                    var beneficiaries = model.Beneficiaries
                        .Select(i => new DitchBeneficiary(i.Account, i.Weight))
                        .ToArray();
                    ops = new BaseOperation[]
                    {
                        op,
                        new BeneficiariesOperation(model.Login, model.Permlink,new Asset(1000000000,3,"SBD") ,beneficiaries)
                    };
                }
                else
                {
                    ops = new BaseOperation[] { op };
                }

                var resp = _operationManager.BroadcastOperationsSynchronous(keys, ct, ops);

                var result = new OperationResult<VoidResponse>();
                if (!resp.IsError)
                {
                    result.Result = new VoidResponse();
                }
                else
                    OnError(resp, result);

                return result;
            }, ct);
        }

        public override async Task<OperationResult<VoidResponse>> Delete(DeleteModel model, CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                if (!TryReconnectChain(ct))
                    return new OperationResult<VoidResponse>(new AppError(LocalizationKeys.EnableConnectToBlockchain));

                var keys = ToKeyArr(model.PostingKey);
                if (keys == null)
                    return new OperationResult<VoidResponse>(new AppError(LocalizationKeys.WrongPrivatePostingKey));

                var op = new DeleteCommentOperation(model.Author, model.Permlink);
                var resp = _operationManager.BroadcastOperationsSynchronous(keys, ct, op);

                var result = new OperationResult<VoidResponse>();
                if (!resp.IsError)
                    result.Result = new VoidResponse();
                else
                    OnError(resp, result);
                return result;
            }, ct);
        }

        public override async Task<OperationResult<VoidResponse>> UpdateUserProfile(UpdateUserProfileModel model, CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                if (!TryReconnectChain(ct))
                    return new OperationResult<VoidResponse>(new AppError(LocalizationKeys.EnableConnectToBlockchain));

                var keys = ToKeyArr(model.ActiveKey);
                if (keys == null)
                    return new OperationResult<VoidResponse>(new AppError(LocalizationKeys.WrongPrivateActimeKey));

                var resp = _operationManager.LookupAccountNames(new[] { model.Login }, CancellationToken.None);
                var result = new OperationResult<VoidResponse>();
                if (resp.IsError)
                {
                    OnError(resp, result);
                    return result;
                }

                var profile = resp.Result.Length == 1 ? resp.Result[0] : null;
                if (profile == null)
                {
                    result.Error = new BlockchainError(LocalizationKeys.UnexpectedProfileData);
                    return result;
                }

                var editedMeta = UpdateProfileJson(profile.JsonMetadata, model);

                var op = new AccountUpdateOperation(model.Login, profile.MemoKey, editedMeta);
                var resp2 = _operationManager.BroadcastOperationsSynchronous(keys, ct, op);
                if (!resp2.IsError)
                    result.Result = new VoidResponse();
                else
                    OnError(resp2, result);
                return result;
            }, ct);
        }

        #endregion Post requests

        #region Get
        public override async Task<OperationResult<object>> GetVerifyTransaction(AuthorizedModel model, CancellationToken ct)
        {
            if (!TryReconnectChain(ct))
                return new OperationResult<object>(new AppError(LocalizationKeys.EnableConnectToBlockchain));

            var keys = ToKeyArr(model.PostingKey);
            if (keys == null)
                return new OperationResult<object>(new AppError(LocalizationKeys.WrongPrivatePostingKey));

            return await Task.Run(() =>
            {
                var op = new FollowOperation(model.Login, "steepshot", DitchFollowType.Blog, model.Login);
                var properties = new DynamicGlobalPropertyObject
                {
                    HeadBlockId = Hex.ToString(_operationManager.ChainId),
                    Time = DateTime.Now,
                    HeadBlockNumber = 0
                };
                var tr = _operationManager.CreateTransaction(properties, keys, ct, op);
                return new OperationResult<object>() { Result = tr };
            }, ct);
        }

        #endregion
    }
}
