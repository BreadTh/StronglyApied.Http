using RestSharp;

namespace BreadTh.StronglyApied.Http
{
    public delegate TransformOutcome<OUTPUT_MODEL> ModelTransformer<OUTPUT_MODEL, INPUT_BODY_MODEL>(
        int retryCount, IRestResponse restResponse, INPUT_BODY_MODEL body);
}
