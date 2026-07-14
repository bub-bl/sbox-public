using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sandbox.Internal;
using System.IO;
using System.Reflection;

namespace HotloadTests;

[TestClass, DoNotParallelize]
public class PackageLoaderTests
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

	private static LoadedAssembly CreateLoadedAssembly( Assembly assembly, byte[] bytes )
	{
		return new LoadedAssembly
		{
			Name = assembly.GetName().Name,
			Version = assembly.GetName().Version,
			Assembly = assembly,
			CompiledAssemblyBytes = bytes
		};
	}

	[TestMethod]
	public void DependentAssemblyFullyHotloadsWhenDependencyWasReplacedAtSameVersion()
	{
		var suffix = System.Guid.NewGuid().ToString( "N" );
		var dependencyName = $"package.test.library_{suffix}";
		var addonName = $"package.test.addon_{suffix}";
		var dependencySource = """
			[assembly: System.Reflection.AssemblyVersion( "1.0.0.0" )]
			namespace TestPackage;
			public class Dependency { }
			""";
		var oldDependencyBytes = CompileAssembly( dependencyName, dependencySource );
		var newDependencyBytes = CompileAssembly( dependencyName, dependencySource );
		var addonBytes = CompileAssembly( addonName, """
			[assembly: System.Reflection.AssemblyVersion( "1.0.0.0" )]
			[assembly: Sandbox.SupportsILHotloadAttribute( "1.0.0.0" )]
			namespace TestPackage;
			public class Addon : Dependency { }
			""",
			MetadataReference.CreateFromImage( newDependencyBytes ),
			MetadataReference.CreateFromFile( typeof( SupportsILHotloadAttribute ).Assembly.Location ) );

		using var packageLoader = new PackageLoader( nameof( DependentAssemblyFullyHotloadsWhenDependencyWasReplacedAtSameVersion ), GetType().Assembly );
		var oldContext = new LoadContext( GetType().Assembly );
		var newContext = new LoadContext( GetType().Assembly );
		var fastHotloadEnabled = HotloadManager.hotload_fast;

		try
		{
			var oldDependency = oldContext.LoadWithEmbeds( oldDependencyBytes );
			var oldAddon = oldContext.LoadWithEmbeds( addonBytes );
			var newDependency = newContext.LoadWithEmbeds( newDependencyBytes );
			var newAddon = newContext.LoadWithEmbeds( addonBytes );

			HotloadManager.hotload_fast = false;
			packageLoader.AddAssembly( CreateLoadedAssembly( oldDependency, oldDependencyBytes ) );
			packageLoader.AddAssembly( CreateLoadedAssembly( oldAddon, addonBytes ) );
			var dependencyResult = packageLoader.AddAssembly( CreateLoadedAssembly( newDependency, newDependencyBytes ) );

			Assert.IsTrue( dependencyResult.FullHotload );
			Assert.AreEqual( oldDependency.GetName().Version, newDependency.GetName().Version );

			HotloadManager.hotload_fast = true;
			var addonResult = packageLoader.AddAssembly( CreateLoadedAssembly( newAddon, addonBytes ) );

			Assert.AreSame( newAddon, addonResult.Assembly );
			Assert.IsTrue( addonResult.FullHotload );
			Assert.IsFalse( addonResult.FastHotload );
		}
		finally
		{
			HotloadManager.hotload_fast = fastHotloadEnabled;
			oldContext.Unload();
			newContext.Unload();
		}
	}
}
