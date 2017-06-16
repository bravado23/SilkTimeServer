﻿/*
 * РЕГИСТРАЦИЯ
 * Created by
 * Roman Khusnetdinov
*/

using System;
using System.Text;
using System.Security.Cryptography;

using ExitGames.Logging;
using Photon.SocketServer;
using Photon.SocketServer.ServerToServer;

using NHibernate.Criterion;

using SilkServerCommon;
using SilkServer.Server2Server;
using SilkServer.SubServer.NHibernate;
using SilkServer.SubServer.NHibernate.Models;

namespace SilkServer.SubServer.Handlers.LoginServer
{
	public class RegisterRequestHandler : PhotonRequestHandler
	{
		protected ILogger Log = LogManager.GetCurrentClassLogger();

		public RegisterRequestHandler(OutboundS2SPeer peer) : base(peer)
		{
		}

		public override void OnHandleRequest(OperationRequest operationRequest)
		{
			var username = (string)operationRequest.Parameters[(byte)UnityParameterCode.Username];
			var password = (string)operationRequest.Parameters[(byte)UnityParameterCode.Password];

			Log.DebugFormat("Register request. Username - {0} | Password - {1}", username, password);

			try
			{
				using (var _session = NHibernateHelper.OpenSession())
				{
					using (var transaction = _session.BeginTransaction())
					{
						var user = _session.CreateCriteria<User>("plu").Add(Restrictions.Eq("plu.Username", username)).UniqueResult<User>();

						// Если пользователь найден, отправляем ошибку - имя пользователя уже используется
						if (user != null)
						{
							_peer.SendOperationResponse(new OperationResponse(operationRequest.OperationCode) { ReturnCode = (short)ErrorCode.UsernameInUse, DebugMessage = "Username is already in use" }, new SendParameters());
							transaction.Commit();
							return;
						}

						// Регистрируем пользователя если мы не нашли совпадений по имени
						var salt = Guid.NewGuid().ToString().Replace("-", "");
						User NewUser = new User()
						{
							Algorithm = "sha256",
							Username = username,
							Salt = salt,
							Password = BitConverter.ToString(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(salt + password))).Replace("-", ""),
							Created = DateTime.Now,
							Updated = DateTime.Now
						};

						_session.Save(NewUser);
						transaction.Commit();

						Log.DebugFormat("Created user {0}", NewUser.Username);

						// Ответ после регистрации
						_peer.SendOperationResponse(new OperationResponse(operationRequest.OperationCode) { ReturnCode = (short)ErrorCode.Ok }, new SendParameters());
						return;
					}
				}
			}
			catch (Exception e)
			{
				Log.ErrorFormat("Error registering user - {0}", e);
			}

			_peer.SendOperationResponse(new OperationResponse(operationRequest.OperationCode) { ReturnCode = (short)ErrorCode.UnknownError }, new SendParameters());
		}
	}
}