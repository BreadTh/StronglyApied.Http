using System.Threading;
using System.Collections.Generic;

using RestSharp;
using System;
using System.Linq;
using Newtonsoft.Json;
using System.Net;

namespace BreadTh.StronglyApied.Http
{
    public class RestTransformer : IRestTransformer
    {

        
        public static Transformer<OUTPUT_MODEL> CreateModelTransformer<OUTPUT_MODEL, INPUT_BODY_MODEL>(
            ModelTransformer<OUTPUT_MODEL, INPUT_BODY_MODEL> transform)
        {
            TransformOutcome<OUTPUT_MODEL> Result(int retryCount, IRestResponse restResponse)
            {
                (INPUT_BODY_MODEL model, List<ErrorDescription> validationErrors) =
                    new ModelValidator().Parse<INPUT_BODY_MODEL>(restResponse.Content);

                if (validationErrors.Count == 0)
                    return transform(retryCount, restResponse, model);
                else
                    return new Next(JsonConvert.SerializeObject(validationErrors));
            }

            return Result;
        }

        public static Transformer<BODY_MODEL> CreateBodyFetcher<BODY_MODEL>() =>
            CreateModelTransformer<BODY_MODEL, BODY_MODEL>(
                (int _, IRestResponse _, BODY_MODEL body) => 
                    Success<BODY_MODEL>.From(body));       

        public static TransformOutcome<string> AcceptAnything(int retryCount, IRestResponse restResponse) =>
            Success<string>.From("ok");

        public static TransformOutcome<string> AcceptAny2xx(int retryCount, IRestResponse restResponse)
        {
            if((int)restResponse.StatusCode >= 200 && (int)restResponse.StatusCode < 300)
                return Success<string>.From("ok");
            else
                return new Next("Not 2xx");
        }

        public static TransformOutcome<OUTPUT_MODEL> DefaultHandleTransportError<OUTPUT_MODEL>(int retryCount, IRestResponse response)
        {
            const int attempts = 10;

            if (response.ResponseStatus != ResponseStatus.Completed)
                if (retryCount >= attempts)
                    return Abort.From(
                        $"Transport error. ResponseStatus was still {response.ResponseStatus} after {attempts} attempts. "
                    +   $"restSharp error message: {response.ErrorMessage}");
                else
                    return Retry.From(TimeSpan.FromMilliseconds(0));

            return new Next("Not TransportError");
        }

        public static TransformOutcome<OUTPUT_MODEL> DefaultHandleHttpStatus5xx<OUTPUT_MODEL>(
            int retryCount, IRestResponse response)
        {
            if ((int)response.StatusCode >= 500 && (int)response.StatusCode <= 599)
                if (retryCount >= defaultAttempts)
                    return Abort.From($"Response HTTP status was still {response.StatusCode} after {defaultAttempts} attempts.");
                else
                    return Retry.From(TimeSpan.FromMilliseconds(defaultBackoffTimes[retryCount]));

            return new Next("Not 5xx");
        }

        public static TransformOutcome<OUTPUT_MODEL> DefaultRetryHandle429<OUTPUT_MODEL>(int retryCount, IRestResponse response)
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                if (retryCount >= defaultAttempts)
                    return Abort.From($"Response HTTP status was still {response.StatusCode} after {defaultAttempts} attempts.");
                else
                    return Retry.From(TimeSpan.FromMilliseconds(defaultBackoffTimes[retryCount]));

            return new Next("Not 429");
        }

        public static TransformOutcome<OUTPUT_MODEL> ReturnDefaultOn404<OUTPUT_MODEL>(int _, IRestResponse response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
                return Success<OUTPUT_MODEL>.From(default);       

            return new Next("Not 404");
        }

        const int defaultAttempts = 5;
        private static int[] defaultBackoffTimes = new int[] { 0, 1_000, 5_000, 10_000, 45_000 };

        private static ulong callnumberCounter = 0;

        IRestClient _client;

        public RestTransformer(IRestClient client)
        {
            _client = client;
        }
        public OUTPUT_MODEL Execute<OUTPUT_MODEL>(IRestRequest request, Transformer<OUTPUT_MODEL> transformer) =>
            Execute(request, new Transformer<OUTPUT_MODEL>[] { transformer });

        public OUTPUT_MODEL Execute<OUTPUT_MODEL>(IRestRequest request, params Transformer<OUTPUT_MODEL>[] transformers)
        {
            int retryCount = 0;

        retry:

            var nextResponseReasons = new List<string>();
            IRestResponse response;

            response = _client.Execute(request);

            foreach (Transformer<OUTPUT_MODEL> transformer in transformers)
            {
                TransformOutcome<OUTPUT_MODEL> successOrNextOrRetryOrAbort = transformer(retryCount, response);

                if (successOrNextOrRetryOrAbort.TryPickT0(out Success<OUTPUT_MODEL> success, out var nextOrRetryOrAbort))
                {
                    Console.WriteLine($"Http call number {++callnumberCounter} successfully parsed.");
                    return success.Value;
                }

                else if (nextOrRetryOrAbort.TryPickT0(out Next next, out var retryOrAbort))
                {
                    nextResponseReasons.Add(next.reason);
                    continue;
                }

                else if (retryOrAbort.TryPickT0(out Retry retry, out Abort abort))
                {
                    Console.WriteLine($"Sleeping for {retry.Value.TotalSeconds} seconds.");
                    Thread.Sleep(retry.Value);
                    retryCount++;
                    goto retry;
                }
                else
                    throw new TransformerSignaledAbortException(abort.Value);
            }

            throw new OutOfTransformersException(
              $"No {ResolveGenericDisplayName(typeof(Transformer<OUTPUT_MODEL>))} was able to parse the rest call response:\n"
            + $"StatusCode: {response.StatusCode}\n"    
            + $"Headers: \n{string.Join('\n', response.Headers.Select(header => header.Name + ": " + header.Value))}\n"
            + $"Body: \n{response.Content}\n"
            + $"reasons: \n{string.Join('\n', nextResponseReasons)}\n");
        }


        private static string ResolveGenericDisplayName(Type type)
        {
            if(!type.IsGenericType)
                return type.Name;

            return type.GetGenericTypeDefinition().Name.Split('`')[0]
                + "<" + string.Join(", ", type.GetGenericArguments().Select((childType) => ResolveGenericDisplayName(childType))) + ">";
        }
    }
}

