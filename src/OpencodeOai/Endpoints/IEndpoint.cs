namespace OpencodeOai.Endpoints;

/// <summary>Marker for a self-contained endpoint module.</summary>
public interface IEndpoint
{
    void Map(IEndpointRouteBuilder app);
}
