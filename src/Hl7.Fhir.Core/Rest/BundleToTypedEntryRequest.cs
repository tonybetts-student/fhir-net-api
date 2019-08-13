﻿/* 
 * Copyright (c) 2014, Firely (info@fire.ly) and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/FirelyTeam/fhir-net-api/master/LICENSE
 */

using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Utility;
using System.Collections.Generic;
using System.Net.Http;

namespace Hl7.Fhir.Rest
{
    public static class BundleToEntryRequest
    {
        public static EntryRequest ToTypedEntryResponse(this Bundle.EntryComponent entry, FhirClientSettings settings)
        {
            var result = new EntryRequest
            {
                Agent = ModelInfo.Version,
                Method = (HTTPVerb)entry.Request.Method,
                Type = entry.Annotation<InteractionType>(),
                Url = entry.Request.Url,
                Headers = new EntryRequestHeaders
                {
                    IfMatch = entry.Request.IfMatch,
                    IfModifiedSince = entry.Request.IfModifiedSince,
                    IfNoneExist = entry.Request.IfNoneExist,
                    IfNoneMatch = entry.Request.IfNoneMatch
                }
            };

            if (entry.Resource != null)
            {
                bool searchUsingPost =
                    result.Method == HTTPVerb.POST
                    && (entry.HasAnnotation<InteractionType>()
                    && entry.Annotation<InteractionType>() == InteractionType.Search)
                    && entry.Resource is Parameters;
                setBodyAndContentType(result, entry.Resource, settings.PreferredFormat, searchUsingPost);
            }

            return result;
        }

        private static void setBodyAndContentType(EntryRequest request, Resource data, ResourceFormat format, bool searchUsingPost)
        {
            if (data == null) throw Error.ArgumentNull(nameof(data));

            if (data is Binary)
            {
                var bin = (Binary)data;
                request.RequestBodyContent = bin.Content;
                // This is done by the caller after the OnBeforeRequest is called so that other properties
                // can be set before the content is committed
                // request.WriteBody(CompressRequestBody, bin.Content);
                request.ContentType = bin.ContentType;
            }
            else if (searchUsingPost)
            {
                IDictionary<string, string> bodyParameters = new Dictionary<string, string>();
                foreach (Parameters.ParameterComponent parameter in ((Parameters)data).Parameter)
                {
                    bodyParameters.Add(parameter.Name, parameter.Value.ToString());
                }
                if (bodyParameters.Count > 0)
                {
                    FormUrlEncodedContent content = new FormUrlEncodedContent(bodyParameters);
                    request.RequestBodyContent = content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                }
                else
                {
                    request.RequestBodyContent = null;
                }

                request.ContentType = "application/x-www-form-urlencoded";
            }
            else
            {
                request.RequestBodyContent = format == ResourceFormat.Xml ?
                    new FhirXmlSerializer().SerializeToBytes(data, summary: Fhir.Rest.SummaryType.False) :
                    new FhirJsonSerializer().SerializeToBytes(data, summary: Fhir.Rest.SummaryType.False);

                // This is done by the caller after the OnBeforeRequest is called so that other properties
                // can be set before the content is committed
                // request.WriteBody(CompressRequestBody, body);
                request.ContentType = Hl7.Fhir.Rest.ContentType.BuildContentType(format, forBundle: false);
            }
        }
    }
}