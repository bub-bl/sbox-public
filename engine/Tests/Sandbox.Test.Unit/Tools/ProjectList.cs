using Sandbox.DataModel;
using System;

namespace ToolsTests;

[TestClass]
public class ProjectListTests
{
	[TestMethod]
	public void FindsMostRecentlyOpenedLocalGame()
	{
		var olderGame = CreateProject( "game", "facepunch", "sandbox", new DateTimeOffset( 2026, 1, 1, 0, 0, 0, TimeSpan.Zero ) );
		var newerGame = CreateProject( "game", "facepunch", "sandbox", new DateTimeOffset( 2026, 2, 1, 0, 0, 0, TimeSpan.Zero ) );
		var addon = CreateProject( "addon", "facepunch", "sandbox", new DateTimeOffset( 2026, 3, 1, 0, 0, 0, TimeSpan.Zero ) );

		var result = Editor.ProjectList.FindLocalGame( [olderGame, addon, newerGame], "facepunch.sandbox" );

		Assert.AreSame( newerGame, result );
	}

	[TestMethod]
	public void AcceptsLocalPackageSuffix()
	{
		var game = CreateProject( "game", "facepunch", "sandbox", DateTimeOffset.UtcNow );

		var result = Editor.ProjectList.FindLocalGame( [game], "facepunch.sandbox#local" );

		Assert.AreSame( game, result );
	}

	private static Project CreateProject( string type, string org, string ident, DateTimeOffset lastOpened )
	{
		return new Project
		{
			Config = new ProjectConfig { Type = type, Org = org, Ident = ident },
			LastOpened = lastOpened
		};
	}
}
