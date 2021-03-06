﻿/*
 * Created by
 * roman.khusnetdinov
*/

using System;
using System.Collections.Generic;

using ExitGames.Logging;

using Photon.SocketServer;
using Photon.SocketServer.ServerToServer;
using PhotonHostRuntimeInterfaces;

using SilkServer.Server2Server;
using SilkServer.SubServer.Handlers;
using SilkServer.Server2Server.Operations;
using SilkServerCommon;
using SilkServer.GameLogic.WorldSystem;
using SilkServer.GameLogic.Client;

namespace SilkServer.MasterServer
{
	public class IncomingSubServerPeer : InboundS2SPeer
	{
		#region Constants and Fields

		public Dictionary<byte, PhotonRequestHandler> RequestHandlers = new Dictionary<byte, PhotonRequestHandler>();
		public Dictionary<byte, PhotonResponseHandler> ResponseHandlers = new Dictionary<byte, PhotonResponseHandler>();
		public Dictionary<byte, PhotonEventHandler> EventHandlers = new Dictionary<byte, PhotonEventHandler>();

		private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

		private readonly MasterServer _server;

		#endregion

		#region Properties

		public Guid? ServerId { get; protected set; }

		public string UdpAddress { get; protected set; }

		public string TcpAddress { get; protected set; }

		public ServerType ServerType { get; protected set; }

		#endregion

		#region Constructors and Destructors

		public IncomingSubServerPeer(InitRequest initRequest, MasterServer server)
			: base(initRequest)
		{
			_server = server;

			if (Log.IsDebugEnabled)
			{
				Log.DebugFormat("SubServer(id={2}) connected from {0}:{1}", RemoteIP, RemotePort, ConnectionId);
			}
		}

		#endregion

		#region Overides of InboundS2SPeer

		protected override void OnDisconnect(DisconnectReason reasonCode, string reasonDetail)
		{
			if (Log.IsDebugEnabled)
			{
				Log.DebugFormat("SubServer(id={0}) connection closed", ConnectionId);
			}

			_server.SubServers.OnDisconnect(this);
		}

		protected override void OnEvent(IEventData eventData, SendParameters sendParameters)
		{
			throw new NotImplementedException();
		}

		protected override void OnOperationRequest(OperationRequest operationRequest, SendParameters sendParameters)
		{
			if (Log.IsDebugEnabled)
			{
				Log.DebugFormat("OnOperationRequest: pid={0}, op={1}", ConnectionId, operationRequest.OperationCode);
			}

			OperationResponse response = new OperationResponse();

			switch ((OperationCode)operationRequest.OperationCode)
			{
				case OperationCode.RegisterSubServer:
					response = ServerId.HasValue
						? new OperationResponse(operationRequest.OperationCode) { ReturnCode = (short)ErrorCode.OperationInvalid, DebugMessage = "Already Registered" }
						: HandleRegisterSubServerRequest(operationRequest);
					break;

				default:
					response = new OperationResponse(operationRequest.OperationCode)
					{
						ReturnCode = (short)ErrorCode.OperationInvalid,
						DebugMessage = "Unknown Operation Request"
					};
					break;
			}

			SendOperationResponse(response, sendParameters);
		}

		protected override void OnOperationResponse(OperationResponse operationResponse, SendParameters sendParameters)
		{
			switch (operationResponse.OperationCode)
			{
				// DataBase Server
				case (byte)UnitySubOperationCode.LoginSecurely:
					HandleLoginSecurely(operationResponse, sendParameters);
					break;
				case (byte)UnitySubOperationCode.RegisterSecurely:
					HandleRegisterSecurely(operationResponse, sendParameters);
					break;
			}
		}

		#endregion

		#region Handlers

		private OperationResponse HandleRegisterSubServerRequest(OperationRequest operationRequest)
		{
			var registerRequest = new RegisterSubServer(Protocol, operationRequest);

			if (!registerRequest.IsValid)
			{
				string msg = registerRequest.GetErrorMessage();

				if (Log.IsDebugEnabled)
				{
					Log.ErrorFormat("RegisterSubServer contract error: {0}", msg);
				}

				return new OperationResponse(operationRequest.OperationCode) { DebugMessage = msg, ReturnCode = (short)ErrorCode.OperationInvalid };
			}

			if (registerRequest.UdpPort.HasValue)
			{
				UdpAddress = registerRequest.SubServerAddress + ":" + registerRequest.UdpPort;
			}

			if (registerRequest.TcpPort.HasValue)
			{
				TcpAddress = registerRequest.SubServerAddress + ":" + registerRequest.TcpPort;
			}

			ServerId = new Guid(registerRequest.ServerId);

			ServerType = (ServerType)registerRequest.ServerType;

			_server.SubServers.OnConnect(this);

			return new OperationResponse(operationRequest.OperationCode);
		}

		private void HandleLoginSecurely(OperationResponse operationResponse, SendParameters sendParameters)
		{
			if (operationResponse.Parameters.ContainsKey((byte)ParameterCode.UserId))
			{
				UnityClient client;

				string UserId = operationResponse.Parameters[(byte)ParameterCode.UserId].ToString();
				_server.ConnectedClients.TryGetValue(new Guid(UserId), out client);

				if (client != null)
				{
					var returnCode = (UnityErrorCode)operationResponse.ReturnCode;
					if (returnCode == UnityErrorCode.ok)
					{
						if (!World.Instance.TryJoin(client))
						{
							client.SendOperationResponse(new OperationResponse(operationResponse.OperationCode = (byte)UnitySubOperationCode.LoginSecurely)
							{
								ReturnCode = (byte)UnityErrorCode.UserAlreadyInGame
							}, new SendParameters());
							return;
						}

						client.Username = (string)operationResponse.Parameters[(byte)UnityParameterCode.Username];
						client.CharacterType = (int)operationResponse.Parameters[(byte)UnityParameterCode.CharacterType];
						client.Money = (int)operationResponse.Parameters[(byte)UnityParameterCode.Money];
						client.Wins = (int)operationResponse.Parameters[(byte)UnityParameterCode.Wins];
						client.Defeats = (int)operationResponse.Parameters[(byte)UnityParameterCode.Defeats];
						client.Kills = (int)operationResponse.Parameters[(byte)UnityParameterCode.Kills];
						client.Deaths = (int)operationResponse.Parameters[(byte)UnityParameterCode.Deaths];
					}

					operationResponse.Parameters.Remove((byte)ParameterCode.UserId);
					client.SendOperationResponse(operationResponse, new SendParameters());
				}
			}
		}

		private void HandleRegisterSecurely(OperationResponse operationResponse, SendParameters sendParameters)
		{
			if (operationResponse.Parameters.ContainsKey((byte)ParameterCode.UserId))
			{
				UnityClient client;

				string UserId = operationResponse.Parameters[(byte)ParameterCode.UserId].ToString();
				_server.ConnectedClients.TryGetValue(new Guid(UserId), out client);

				if (client != null)
				{
					var returnCode = (UnityErrorCode)operationResponse.ReturnCode;
					if (returnCode == UnityErrorCode.ok)
					{
						if (!World.Instance.TryJoin(client))
						{
							client.SendOperationResponse(new OperationResponse(operationResponse.OperationCode = (byte)UnitySubOperationCode.RegisterSecurely)
							{
								ReturnCode = (byte)UnityErrorCode.UserAlreadyInGame
							}, new SendParameters());
							return;
						}

						client.Username = (string)operationResponse.Parameters[(byte)UnityParameterCode.Username];
						client.CharacterType = (int)operationResponse.Parameters[(byte)UnityParameterCode.CharacterType];
						client.Money = (int)operationResponse.Parameters[(byte)UnityParameterCode.Money];
						client.Wins = (int)operationResponse.Parameters[(byte)UnityParameterCode.Wins];
						client.Defeats = (int)operationResponse.Parameters[(byte)UnityParameterCode.Defeats];
						client.Kills = (int)operationResponse.Parameters[(byte)UnityParameterCode.Kills];
						client.Deaths = (int)operationResponse.Parameters[(byte)UnityParameterCode.Deaths];
					}

					operationResponse.Parameters.Remove((byte)ParameterCode.UserId);
					client.SendOperationResponse(operationResponse, new SendParameters());
				}
			}
		}

		#endregion
	}
}
