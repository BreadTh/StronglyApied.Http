using OneOf;

namespace BreadTh.StronglyApied.Http
{
    public class TransformOutcome<T> : OneOfBase<Success<T>, Next, Retry, Abort> {
	    TransformOutcome(OneOf<Success<T>, Next, Retry, Abort> x) : base(x) { }
	    public static implicit operator TransformOutcome<T>(Success<T> x) => new TransformOutcome<T>(x);
	    public static implicit operator TransformOutcome<T>(Next x) => new TransformOutcome<T>(x);
	    public static implicit operator TransformOutcome<T>(Retry x) => new TransformOutcome<T>(x);
	    public static implicit operator TransformOutcome<T>(Abort x) => new TransformOutcome<T>(x);
    }
}

