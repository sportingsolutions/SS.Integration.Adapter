<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="SupervisorView.aspx.cs" Inherits="SS.Integration.Adapter.Diagnostics.UI.SupervisorView" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title></title>
</head>

<script src="Scripts/jquery-1.6.4.js"></script>
<script src="Scripts/jquery.signalR-2.1.2.js"></script>
<script src="http://localhost:1234/signalr/hubs"></script>

<script type="text/javascript">
    $(function () {
        $.connection.hub.url = "http://localhost:1234/signalr";
        var hub = $.connection.supervisorHub;
        
        hub.client.publish = function (a) { $('#testControl').text(a.Id); };
        
        $.connection.hub.start().done(function() {
            $('#testControl').text("Connected");
        });
    })
</script>    

<body>
    <form id="form1" runat="server">
    <div>
    <div id="testControl" style="font-size: 20px;">Test</div>
    </div>
    </form>
</body>
</html>
