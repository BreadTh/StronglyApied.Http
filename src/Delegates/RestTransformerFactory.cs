using RestSharp;

namespace BreadTh.StronglyApied.Http
{
    delegate RestTransformer RestTransformerFactory(IRestClient restClient);
}
