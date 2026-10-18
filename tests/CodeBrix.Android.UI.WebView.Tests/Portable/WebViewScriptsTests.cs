using System;
using System.Text.Json;
using CodeBrix.Android.UI.WebView.Portable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.WebView.Tests.Portable;

public class WebViewScriptsTests
{
    [Fact]
    public void An_invocation_passes_its_arguments_as_JSON_strings()
    {
        //Arrange
        //Act
        var call = WebViewScripts.Invocation("greet", ["Ada", "a \"quoted\" name"]);

        //Assert
        call.Should().Be("greet(\"Ada\",\"a \\u0022quoted\\u0022 name\")");
    }

    [Fact]
    public void An_invocation_without_arguments_is_an_empty_call()
    {
        //Arrange
        //Act
        var call = WebViewScripts.Invocation("refresh", null);

        //Assert
        call.Should().Be("refresh()");
    }

    [Fact]
    public void The_document_start_script_gives_a_page_both_names_it_posts_through()
    {
        //Arrange
        //Act
        var script = WebViewScripts.DocumentStartScript;

        //Assert
        script.Should().Contain("window.chrome.webview.postMessage=post");
        script.Should().Contain("window.webkit.messageHandlers.codebrixWebView={postMessage:post}");
        script.Should().Contain("window." + WebViewScripts.BridgeObjectName);
        script.Should().Contain("JSON.stringify");
    }

    [Fact]
    public void The_document_start_script_gives_a_page_the_host_to_page_listener_surface()
    {
        //Arrange
        //Act
        var script = WebViewScripts.DocumentStartScript;

        //Assert
        script.Should().Contain("v.addEventListener=function(t,f){if(t==='message'");
        script.Should().Contain("v.removeEventListener=function(t,f){if(t==='message')");
        script.Should().Contain("v.onmessage");
        script.Should().Contain("Object.defineProperty(v,'" + WebViewScripts.DispatchFunctionName + "'");
        script.Should().Contain("data:d");
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("a \"quoted\" 'single' text")]
    [InlineData("two\nlines\r\nand a tab\t")]
    [InlineData("unicode: \u00e9 \u4e2d \U0001F600 \u2028 \u2029")]
    [InlineData("</script><script>alert(1)</script>")]
    [InlineData("back\\slash and ${template} and `tick`")]
    public void A_string_message_reaches_the_page_only_as_a_JSON_string_literal(string message)
    {
        //Arrange
        //Act
        var script = WebViewScripts.StringMessageDelivery(message);
        var literal = DispatchArgument(script);

        //Assert
        JsonSerializer.Deserialize<string>(literal).Should().Be(message);
        literal.Should().NotContain("<");
        literal.Should().NotContain(">");
        literal.Should().NotContain("\n");
        literal.Should().NotContain("\u2028");
        literal.Should().NotContain("\u2029");
        literal.Should().NotContain("`");
    }

    [Fact]
    public void A_null_string_message_is_sent_as_an_empty_string()
    {
        //Arrange
        //Act
        var literal = DispatchArgument(WebViewScripts.StringMessageDelivery(null));

        //Assert
        literal.Should().Be("\"\"");
    }

    [Theory]
    [InlineData("{\"a\":[1,2,{\"b\":\"</script>\"}]}")]
    [InlineData("\"just a string\"")]
    [InlineData("42")]
    [InlineData("null")]
    [InlineData("[\"line\\nbreak\", \"\u00e9\"]")]
    public void A_JSON_message_is_parsed_by_the_page_never_evaluated(string json)
    {
        //Arrange
        //Act
        var script = WebViewScripts.JsonMessageDelivery(json);
        var argument = DispatchArgument(script);

        //Assert
        argument.Should().StartWith("JSON.parse(");
        argument.Should().EndWith(")");
        var literal = argument.Substring("JSON.parse(".Length, argument.Length - "JSON.parse(".Length - 1);
        JsonSerializer.Deserialize<string>(literal).Should().Be(json);
        literal.Should().NotContain("<");
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"a\":")]
    [InlineData("alert(1)")]
    [InlineData("")]
    public void An_invalid_JSON_message_is_refused(string json)
    {
        //Arrange
        //Act
        Action act = () => WebViewScripts.JsonMessageDelivery(json);

        //Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_null_JSON_message_is_refused()
    {
        //Arrange
        //Act
        Action act = () => WebViewScripts.JsonMessageDelivery(null);

        //Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_delivery_reports_a_page_without_the_channel()
    {
        //Arrange
        //Act
        var script = WebViewScripts.StringMessageDelivery("x");

        //Assert
        script.Should().Contain("typeof v." + WebViewScripts.DispatchFunctionName + "!=='function'){return " + WebViewScripts.NoChannelResult + ";}");
        script.Should().StartWith("(function(){");
        script.Should().EndWith("})()");
    }

    // The argument of the page-side dispatch call: "...v.__codebrixDispatchMessage(<argument>);})()".
    private static string DispatchArgument(string script)
    {
        var call = "return v." + WebViewScripts.DispatchFunctionName + "(";
        var start = script.LastIndexOf(call, StringComparison.Ordinal);
        start.Should().BeGreaterThan(0);
        start += call.Length;
        const string tail = ");})()";
        script.Should().EndWith(tail);
        return script.Substring(start, script.Length - start - tail.Length);
    }
}
