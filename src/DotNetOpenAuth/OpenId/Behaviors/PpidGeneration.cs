﻿//-----------------------------------------------------------------------
// <copyright file="PpidGeneration.cs" company="Andrew Arnott">
//     Copyright (c) Andrew Arnott. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace DotNetOpenAuth.OpenId.Behaviors {
	using System;
	using System.Linq;
	using DotNetOpenAuth.Messaging;
	using DotNetOpenAuth.OpenId.Extensions.ProviderAuthenticationPolicy;
	using DotNetOpenAuth.OpenId.Provider;

	/// <summary>
	/// Offers OpenID Providers automatic PPID Claimed Identifier generation when requested
	/// by a PAPE request.
	/// </summary>
	/// <remarks>
	/// <para>PPIDs are set on positive authentication responses when the PAPE request includes
	/// the <see cref="AuthenticationPolicies.PrivatePersonalIdentifier"/> authentication policy.</para>
	/// <para>The static member <see cref="PpidGeneration.PpidIdentifierProvider"/> MUST
	/// be set prior to any PPID requests come in.  Typically this should be set in the
	/// <c>Application_Start</c> method in the global.asax.cs file.</para>
	/// </remarks>
	[Serializable]
	public sealed class PpidGeneration : IProviderBehavior {
		/// <summary>
		/// Gets or sets the provider for generating PPID identifiers.
		/// </summary>
		public static IDirectedIdentityIdentifierProvider PpidIdentifierProvider { get; set; }

		#region IProviderBehavior Members

		/// <summary>
		/// Called when a request is received by the Provider.
		/// </summary>
		/// <param name="request">The incoming request.</param>
		/// <returns>
		/// 	<c>true</c> if this behavior owns this request and wants to stop other behaviors
		/// from handling it; <c>false</c> to allow other behaviors to process this request.
		/// </returns>
		/// <remarks>
		/// Implementations may set a new value to <see cref="IRequest.SecuritySettings"/> but
		/// should not change the properties on the instance of <see cref="ProviderSecuritySettings"/>
		/// itself as that instance may be shared across many requests.
		/// </remarks>
		bool IProviderBehavior.OnIncomingRequest(IRequest request) {
			return false;
		}

		/// <summary>
		/// Called when the Provider is preparing to send a response to an authentication request.
		/// </summary>
		/// <param name="request">The request that is configured to generate the outgoing response.</param>
		/// <returns>
		/// 	<c>true</c> if this behavior owns this request and wants to stop other behaviors
		/// from handling it; <c>false</c> to allow other behaviors to process this request.
		/// </returns>
		bool IProviderBehavior.OnOutgoingResponse(IAuthenticationRequest request) {
			ErrorUtilities.VerifyArgumentNotNull(request, "request");

			// Nothing to do for negative assertions.
			if (!request.IsAuthenticated.Value) {
				return false;
			}

			var requestInternal = (Provider.AuthenticationRequest)request;
			var responseMessage = (IProtocolMessageWithExtensions)requestInternal.Response;

			// Only apply our special policies if the RP requested it.
			var papeRequest = request.GetExtension<PolicyRequest>();
			if (papeRequest != null) {
				if (papeRequest.PreferredPolicies.Contains(AuthenticationPolicies.PrivatePersonalIdentifier)) {
					ErrorUtilities.VerifyProtocol(request.ClaimedIdentifier == request.LocalIdentifier, OpenIdStrings.DelegatingIdentifiersNotAllowed);

					if (PpidIdentifierProvider == null) {
						Logger.OpenId.Error(BehaviorStrings.PpidProviderNotGiven);
						return false;
					}

					// Mask the user's identity with a PPID.
					if (PpidIdentifierProvider.IsUserLocalIdentifier(request.LocalIdentifier)) {
						Identifier ppidIdentifier = PpidIdentifierProvider.GetIdentifier(request.LocalIdentifier, request.Realm);
						requestInternal.ResetClaimedAndLocalIdentifiers(ppidIdentifier);
					}

					// Indicate that the RP is receiving a PPID claimed_id
					var papeResponse = responseMessage.Extensions.OfType<PolicyResponse>().SingleOrDefault();
					if (papeResponse == null) {
						request.AddResponseExtension(papeResponse = new PolicyResponse());
					}

					if (!papeResponse.ActualPolicies.Contains(AuthenticationPolicies.PrivatePersonalIdentifier)) {
						papeResponse.ActualPolicies.Add(AuthenticationPolicies.PrivatePersonalIdentifier);
					}
				}
			}

			return false;
		}

		#endregion
	}
}