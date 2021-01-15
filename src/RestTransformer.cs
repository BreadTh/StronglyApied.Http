using System.Threading;
using System.Collections.Generic;

using RestSharp;
using System;

namespace BreadTh.StronglyApied.Http
{
    public class RestTransformer : IRestTransformer
    {
        IRestClient _client;
        public RestTransformer(IRestClient client)
        {
            _client = client;
        }

        public static Transformer<OUTPUT_MODEL> CreateModelTransformer<OUTPUT_MODEL, INPUT_BODY_MODEL>(
            ModelTransformer<OUTPUT_MODEL, INPUT_BODY_MODEL> transform)
        {
            TransformOutcome<OUTPUT_MODEL> Result(int retryCount, IRestResponse restResponse)
            {
                (INPUT_BODY_MODEL model, List<ErrorDescription> validationErrors) =
                    new ModelValidator().TryParse<INPUT_BODY_MODEL>(restResponse.Content);

                if (validationErrors.Count == 0)
                    return transform(retryCount, restResponse, model);
                else
                    return new Next();
            }

            return Result;
        }

        public static TransformOutcome<OUTPUT_MODEL> DefaultHandleTransportError<OUTPUT_MODEL>(
            int retryCount, IRestResponse response)
        {
            const int attempts = 10;

            if (response.ResponseStatus != ResponseStatus.Completed)
                if (retryCount >= 10)
                    return Abort.From(
                        $"Transport error. ResponseStatus was still {response.ResponseStatus} after {attempts} attempts. "
                    + $"restSharp error message: {response.ErrorMessage}");
                else
                    return Retry.From(TimeSpan.FromMilliseconds(0));

            return new Next();
        }

        public static TransformOutcome<OUTPUT_MODEL> DefaultHandleHttpStatus5xx<OUTPUT_MODEL>(
            int retryCount, IRestResponse response)
        {
            const int attempts = 5;

            if (retryCount >= attempts)
                return Abort.From(
                    $"Response HTTP status was still {response.StatusCode} after {attempts} attempts.");

            if ((int)response.StatusCode >= 500 && (int)response.StatusCode <= 599)
                return Retry.From(TimeSpan.FromMilliseconds(new int[] { 0, 1_000, 5_000, 10_000, 60_000 }[retryCount]));

            return new Next();
        }

        public OUTPUT_MODEL Execute<OUTPUT_MODEL>(IRestRequest request, params Transformer<OUTPUT_MODEL>[] transformers)
        {
            int retryCount = 0;
        retry:

            IRestResponse response = _client.Execute(request);

            foreach (Transformer<OUTPUT_MODEL> transformer in transformers)
            {
                TransformOutcome<OUTPUT_MODEL> successOrNextOrRetryOrAbort = transformer(retryCount, response);

                if (successOrNextOrRetryOrAbort.TryPickT0(out Success<OUTPUT_MODEL> success, out var nextOrRetryOrAbort))
                    return success.Value;

                else if (nextOrRetryOrAbort.TryPickT0(out Next _, out var retryOrAbort))
                    continue;

                else if (retryOrAbort.TryPickT0(out Retry retry, out Abort abort))
                {
                    Thread.Sleep(retry.Value);
                    retryCount++;
                    goto retry;
                }
                else
                    throw new TransformerSignaledAbortException(abort.Value);
            }

            throw new OutOfTransformersException(
                $"No {typeof(Transformer<OUTPUT_MODEL>).FullName} was available for parsing the rest call response:\n"
            + $"StatusCode: {response.StatusCode}\n"
            + $"Headers: {response.Headers}\n"
            + $"Body: {response.Content}");
        }
    }
}

