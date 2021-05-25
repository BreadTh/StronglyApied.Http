using RestSharp;

namespace BreadTh.StronglyApied.Http
{
    public interface IRestTransformer
    {
        OUTPUT_MODEL Execute<OUTPUT_MODEL>(IRestRequest request, params Transformer<OUTPUT_MODEL>[] transformers);
        OUTPUT_MODEL Execute<OUTPUT_MODEL>(IRestRequest request, Transformer<OUTPUT_MODEL> transformer);
    }
}