using System.Collections.Immutable;

namespace ReflectionTests;

[TestClass]
public class ReflectionUtilityTests
{
	private sealed class ReferencedObject
	{
	}

	private static class ImmutableArrayHolder
	{
		public static ImmutableArray<ReferencedObject> Values;
	}

	[TestMethod]
	public void NullStaticReferencesHandlesImmutableArrays()
	{
		ImmutableArrayHolder.Values = default;
		ReflectionUtility.NullStaticReferencesOfType( typeof( ReflectionUtilityTests ).Assembly, typeof( ReferencedObject ) );
		Assert.IsFalse( ImmutableArrayHolder.Values.IsDefault );
		Assert.IsTrue( ImmutableArrayHolder.Values.IsEmpty );

		ImmutableArrayHolder.Values = [new ReferencedObject()];
		ReflectionUtility.NullStaticReferencesOfType( typeof( ReflectionUtilityTests ).Assembly, typeof( ReferencedObject ) );
		Assert.IsTrue( ImmutableArrayHolder.Values.IsEmpty );
	}
}
