using RestSharp;

namespace BreadTh.StronglyApied.Http
{
    public delegate TransformOutcome<OUTPUT_MODEL> Transformer<OUTPUT_MODEL>(int retryCount, IRestResponse restResponse);
}
