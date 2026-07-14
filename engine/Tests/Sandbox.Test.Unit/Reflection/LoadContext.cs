using Sandbox.Internal;
using System.Reflection;

namespace ReflectionTests;

[TestClass]
public class LoadContextTest
{
	[TestMethod]
	public void MissingRootAssemblyFallsBackToOnDemandResolver()
	{
		var loadContext = new LoadContext( GetType().Assembly );
		var expected = typeof( LoadContextTest ).Assembly;
		var requested = new AssemblyName( $"package.test.missing_{System.Guid.NewGuid():N}" );
		var resolvedName = "";

		loadContext.OnDemandResolver = name =>
		{
			resolvedName = name;
			return expected;
		};

		var resolved = loadContext.LoadFromChild( requested );

		Assert.AreSame( expected, resolved );
		Assert.AreEqual( requested.Name, resolvedName );

		loadContext.Unload();
	}
}
