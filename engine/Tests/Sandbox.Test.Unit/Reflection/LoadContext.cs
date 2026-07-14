using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sandbox.Internal;
using System.IO;
using System.Reflection;

namespace ReflectionTests;

[TestClass]
public class LoadContextTest
{
	private static byte[] CompileAssembly( string assemblyName, string source, params MetadataReference[] additionalReferences )
	{
		var corePath = Path.GetDirectoryName( typeof( object ).Assembly.Location );
		var references = new[]
		{
			MetadataReference.CreateFromFile( typeof( object ).Assembly.Location ),
			MetadataReference.CreateFromFile( Path.Combine( corePath, "System.Runtime.dll" ) )
		}.Concat( additionalReferences );

		var compilation = CSharpCompilation.Create(
			assemblyName,
			[CSharpSyntaxTree.ParseText( source )],
			references,
			new CSharpCompilationOptions( OutputKind.DynamicallyLinkedLibrary ) );

		using var stream = new MemoryStream();
		var result = compilation.Emit( stream );
		Assert.IsTrue( result.Success, string.Join( "\n", result.Diagnostics ) );
		return stream.ToArray();
	}

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

	[TestMethod]
	public void ReferencedAssemblyVersionChangeUsesOnDemandResolver()
	{
		var suffix = System.Guid.NewGuid().ToString( "N" );
		var dependencyName = $"package.test.library_{suffix}";
		var addonName = $"package.test.addon_{suffix}";
		var oldDependency = CompileAssembly( dependencyName, """
			[assembly: System.Reflection.AssemblyVersion( "0.0.117.0" )]
			namespace Sandbox;
			public class Placeholder { }
			""" );
		var newDependency = CompileAssembly( dependencyName, """
			[assembly: System.Reflection.AssemblyVersion( "0.0.118.0" )]
			namespace Sandbox;
			public class InventoryComponent { }
			""" );
		var addon = CompileAssembly( addonName, """
			public class Inventory : Sandbox.InventoryComponent { }
			""", MetadataReference.CreateFromImage( newDependency ) );

		var loadContext = new LoadContext( GetType().Assembly );
		var resolverCalled = false;

		try
		{
			loadContext.LoadWithEmbeds( oldDependency );
			loadContext.OnDemandResolver = name =>
			{
				if ( name != dependencyName ) return null;

				resolverCalled = true;
				return loadContext.LoadWithEmbeds( newDependency, false );
			};

			var addonAssembly = loadContext.LoadWithEmbeds( addon );
			var inventoryType = addonAssembly.GetTypes().Single( x => x.Name == "Inventory" );

			Assert.IsTrue( resolverCalled );
			Assert.AreEqual( new System.Version( 0, 0, 118, 0 ), inventoryType.BaseType.Assembly.GetName().Version );
		}
		finally
		{
			loadContext.Unload();
		}
	}
}
