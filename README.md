# SampleCompletionProviders
Sample of Microsoft.CodeAnalysis.Completion.CompletionProvider interface usage

There are 3 classes implementing it:
* ConfigureAwaitCompletionProvider - suggests `ConfigureAwait(false)` on `Task`, so you don't have type that _open paren, false, close paren_. Described on [DotVVM blog article](https://dotvvm.com/blog)
* PostfixTemplateCompletionProvider - suggests
  - `not` "field" on `bool` witch is expanded to `!(...)` expression after commit
  - `return` field on `void` witch is expanded to return statement following the current statement
* ReflectionGetMemberCompletionProvider - suggests members of type in `typeof(TheType).GetMethod/GetField/GetProperty/GetMember("...")` expression

See article on [DotVVM blog](https://dotvvm.com/blog) for more information how this works. And source code of the individual classes,
they contain a lot of comments although it's agains my practise :).

The full documentation is at http://source.roslyn.io, specificaly have a look at 
[Microsoft.CodeAnalysis.Completion.CompletionProvider](http://source.roslyn.io/#Microsoft.CodeAnalysis.Features/Completion/CompletionProvider.cs,3632bba4d25ec249)
and its implementations
