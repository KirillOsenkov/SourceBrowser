Module M
    Sub S()
        Dim a As Object()
        a = New Object() {}
        Console.WriteLine(a.Length)
        a = New Object(-1) {}
        Console.WriteLine(a.Length)
        Dim e(-1) As Object
        a = e
        Console.WriteLine(a.Length)

        Dim x = <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <X>
                    <n:T></n:T>
                    <X/>
                    <A.B></A.B>
                    <A B=""></A>
                    <A>&#x03C0;</A>
                    <A>a &lt;</>
                    <A><![CDATA[bar]]></A>
                    <%= Nothing %>
                    <!-- comment -->
                </X>
    End Sub
End Module

Namespace Namespace1
    Public Class Class1
        Public ReadOnly Property Property1 As String
    End Class
End Namespace