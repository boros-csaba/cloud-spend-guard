using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.Runtime;

namespace CloudSpendGuard.Functions.Connections;

public static class NetworkCollector
{
    public static async Task<IReadOnlyList<Resource>> CollectVpcs(AWSCredentials credentials, string region, string accountId)
    {
        using var ec2 = new AmazonEC2Client(credentials, RegionEndpoint.GetBySystemName(region));
        var resources = new List<Resource>();

        await foreach (var vpc in ec2.Paginators.DescribeVpcs(new DescribeVpcsRequest()).Vpcs)
        {
            var tags = (vpc.Tags ?? []).ToDictionary(tag => tag.Key, tag => tag.Value);

            resources.Add(new Resource(
                $"arn:aws:ec2:{region}:{accountId}:vpc/{vpc.VpcId}",
                "ec2:vpc",
                region,
                tags.GetValueOrDefault("Name"),
                new Dictionary<string, string>
                {
                    ["vpcId"] = vpc.VpcId,
                    ["cidr"] = vpc.CidrBlock,
                    ["isDefault"] = vpc.IsDefault == true ? "true" : "false",
                },
                tags,
                default,
                null,
                null));
        }

        return resources;
    }

    public static async Task<IReadOnlyList<Resource>> CollectSubnets(AWSCredentials credentials, string region, string accountId)
    {
        using var ec2 = new AmazonEC2Client(credentials, RegionEndpoint.GetBySystemName(region));
        var resources = new List<Resource>();

        await foreach (var subnet in ec2.Paginators.DescribeSubnets(new DescribeSubnetsRequest()).Subnets)
        {
            var tags = (subnet.Tags ?? []).ToDictionary(tag => tag.Key, tag => tag.Value);

            resources.Add(new Resource(
                $"arn:aws:ec2:{region}:{accountId}:subnet/{subnet.SubnetId}",
                "ec2:subnet",
                region,
                tags.GetValueOrDefault("Name"),
                new Dictionary<string, string>
                {
                    ["subnetId"] = subnet.SubnetId,
                    ["vpcId"] = subnet.VpcId,
                    ["availabilityZone"] = subnet.AvailabilityZone,
                    ["cidr"] = subnet.CidrBlock ?? "",
                    ["public"] = subnet.MapPublicIpOnLaunch == true ? "true" : "false",
                },
                tags,
                default,
                null,
                null));
        }

        return resources;
    }

    public static async Task<IReadOnlyList<Resource>> CollectSecurityGroups(AWSCredentials credentials, string region, string accountId)
    {
        using var ec2 = new AmazonEC2Client(credentials, RegionEndpoint.GetBySystemName(region));
        var resources = new List<Resource>();

        await foreach (var group in ec2.Paginators.DescribeSecurityGroups(new DescribeSecurityGroupsRequest { MaxResults = 1000 }).SecurityGroups)
        {
            var tags = (group.Tags ?? []).ToDictionary(tag => tag.Key, tag => tag.Value);

            resources.Add(new Resource(
                group.SecurityGroupArn ?? $"arn:aws:ec2:{region}:{accountId}:security-group/{group.GroupId}",
                "ec2:security-group",
                region,
                tags.GetValueOrDefault("Name") ?? group.GroupName,
                new Dictionary<string, string>
                {
                    ["groupId"] = group.GroupId,
                    ["vpcId"] = group.VpcId ?? "",
                    ["description"] = group.Description ?? "",
                    ["inboundRules"] = (group.IpPermissions?.Count ?? 0).ToString(),
                    ["outboundRules"] = (group.IpPermissionsEgress?.Count ?? 0).ToString(),
                },
                tags,
                default,
                null,
                null));
        }

        return resources;
    }

    public static async Task<IReadOnlyList<Resource>> CollectInternetGateways(AWSCredentials credentials, string region, string accountId)
    {
        using var ec2 = new AmazonEC2Client(credentials, RegionEndpoint.GetBySystemName(region));
        var resources = new List<Resource>();

        await foreach (var gateway in ec2.Paginators.DescribeInternetGateways(new DescribeInternetGatewaysRequest { MaxResults = 1000 }).InternetGateways)
        {
            var tags = (gateway.Tags ?? []).ToDictionary(tag => tag.Key, tag => tag.Value);

            resources.Add(new Resource(
                $"arn:aws:ec2:{region}:{accountId}:internet-gateway/{gateway.InternetGatewayId}",
                "ec2:internet-gateway",
                region,
                tags.GetValueOrDefault("Name"),
                new Dictionary<string, string>
                {
                    ["internetGatewayId"] = gateway.InternetGatewayId,
                    ["vpcId"] = gateway.Attachments is [var attachment, ..] ? attachment.VpcId : "",
                },
                tags,
                default,
                null,
                null));
        }

        return resources;
    }

    public static async Task<IReadOnlyList<Resource>> CollectRouteTables(AWSCredentials credentials, string region, string accountId)
    {
        using var ec2 = new AmazonEC2Client(credentials, RegionEndpoint.GetBySystemName(region));
        var resources = new List<Resource>();

        await foreach (var table in ec2.Paginators.DescribeRouteTables(new DescribeRouteTablesRequest { MaxResults = 100 }).RouteTables)
        {
            var tags = (table.Tags ?? []).ToDictionary(tag => tag.Key, tag => tag.Value);
            var associations = table.Associations ?? [];

            resources.Add(new Resource(
                $"arn:aws:ec2:{region}:{accountId}:route-table/{table.RouteTableId}",
                "ec2:route-table",
                region,
                tags.GetValueOrDefault("Name"),
                new Dictionary<string, string>
                {
                    ["routeTableId"] = table.RouteTableId,
                    ["vpcId"] = table.VpcId ?? "",
                    ["main"] = associations.Any(association => association.Main == true) ? "true" : "false",
                    ["subnets"] = associations.Count(association => association.SubnetId is not null).ToString(),
                    ["routes"] = (table.Routes?.Count ?? 0).ToString(),
                },
                tags,
                default,
                null,
                null));
        }

        return resources;
    }

    public static async Task<IReadOnlyList<Resource>> CollectNatGateways(AWSCredentials credentials, string region, string accountId)
    {
        using var ec2 = new AmazonEC2Client(credentials, RegionEndpoint.GetBySystemName(region));
        var resources = new List<Resource>();
        var routed = new HashSet<string>();

        await foreach (var table in ec2.Paginators.DescribeRouteTables(new DescribeRouteTablesRequest()).RouteTables)
        {
            foreach (var route in table.Routes ?? [])
            {
                if (route.NatGatewayId is not null)
                {
                    routed.Add(route.NatGatewayId);
                }
            }
        }

        await foreach (var gateway in ec2.Paginators.DescribeNatGateways(new DescribeNatGatewaysRequest()).NatGateways)
        {
            if (gateway.State?.Value is "deleting" or "deleted" or "failed")
            {
                continue;
            }

            var tags = (gateway.Tags ?? []).ToDictionary(tag => tag.Key, tag => tag.Value);

            resources.Add(new Resource(
                $"arn:aws:ec2:{region}:{accountId}:natgateway/{gateway.NatGatewayId}",
                "ec2:natgateway",
                region,
                tags.GetValueOrDefault("Name"),
                new Dictionary<string, string>
                {
                    ["natGatewayId"] = gateway.NatGatewayId,
                    ["state"] = gateway.State?.Value ?? "",
                    ["connectivityType"] = gateway.ConnectivityType?.Value ?? "",
                    ["vpcId"] = gateway.VpcId ?? "",
                    ["subnetId"] = gateway.SubnetId ?? "",
                    ["createdAt"] = gateway.CreateTime?.ToString("O") ?? "",
                    ["routed"] = routed.Contains(gateway.NatGatewayId) ? "true" : "false",
                },
                tags,
                default,
                null,
                null));
        }

        return resources;
    }

    public static async Task<IReadOnlyList<Resource>> CollectVpnConnections(AWSCredentials credentials, string region, string accountId)
    {
        using var ec2 = new AmazonEC2Client(credentials, RegionEndpoint.GetBySystemName(region));
        var response = await ec2.DescribeVpnConnectionsAsync(new DescribeVpnConnectionsRequest());
        var resources = new List<Resource>();

        foreach (var connection in response.VpnConnections ?? [])
        {
            if (connection.State?.Value is "deleting" or "deleted")
            {
                continue;
            }

            var tags = (connection.Tags ?? []).ToDictionary(tag => tag.Key, tag => tag.Value);
            var tunnels = connection.VgwTelemetry ?? [];
            var details = new Dictionary<string, string>
            {
                ["vpnConnectionId"] = connection.VpnConnectionId,
                ["state"] = connection.State?.Value ?? "",
                ["gatewayId"] = connection.TransitGatewayId ?? connection.VpnGatewayId ?? "",
                ["customerGatewayId"] = connection.CustomerGatewayId ?? "",
                ["tunnels"] = tunnels.Count.ToString(),
                ["tunnelsUp"] = tunnels.Count(tunnel => tunnel.Status?.Value == "UP").ToString(),
            };

            if (tunnels.Count > 0 && tunnels.All(tunnel => tunnel.Status?.Value == "DOWN") && tunnels.Max(tunnel => tunnel.LastStatusChange) is { } downSince)
            {
                details["tunnelsDownSince"] = downSince.ToString("O");
            }

            resources.Add(new Resource(
                $"arn:aws:ec2:{region}:{accountId}:vpn-connection/{connection.VpnConnectionId}",
                "ec2:vpn-connection",
                region,
                tags.GetValueOrDefault("Name"),
                details,
                tags,
                default,
                null,
                null));
        }

        return resources;
    }

    public static async Task<IReadOnlyList<Resource>> CollectVpcEndpoints(AWSCredentials credentials, string region, string accountId)
    {
        using var ec2 = new AmazonEC2Client(credentials, RegionEndpoint.GetBySystemName(region));
        var resources = new List<Resource>();

        await foreach (var endpoint in ec2.Paginators.DescribeVpcEndpoints(new DescribeVpcEndpointsRequest()).VpcEndpoints)
        {
            if (string.Equals(endpoint.State?.Value, "deleted", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var tags = (endpoint.Tags ?? []).ToDictionary(tag => tag.Key, tag => tag.Value);

            resources.Add(new Resource(
                $"arn:aws:ec2:{region}:{accountId}:vpc-endpoint/{endpoint.VpcEndpointId}",
                "ec2:vpc-endpoint",
                region,
                tags.GetValueOrDefault("Name"),
                new Dictionary<string, string>
                {
                    ["vpcEndpointId"] = endpoint.VpcEndpointId,
                    ["vpcId"] = endpoint.VpcId ?? "",
                    ["serviceName"] = endpoint.ServiceName ?? "",
                    ["endpointType"] = endpoint.VpcEndpointType?.Value ?? "",
                    ["state"] = endpoint.State?.Value ?? "",
                    ["subnets"] = (endpoint.SubnetIds?.Count ?? 0).ToString(),
                    ["createdAt"] = endpoint.CreationTimestamp?.ToString("O") ?? "",
                },
                tags,
                default,
                null,
                null));
        }

        return resources;
    }

    public static async Task<IReadOnlyList<Resource>> CollectTransitGatewayAttachments(AWSCredentials credentials, string region, string accountId)
    {
        using var ec2 = new AmazonEC2Client(credentials, RegionEndpoint.GetBySystemName(region));
        var resources = new List<Resource>();
        var request = new DescribeTransitGatewayAttachmentsRequest();

        await foreach (var attachment in ec2.Paginators.DescribeTransitGatewayAttachments(request).TransitGatewayAttachments)
        {
            if (attachment.State?.Value is "deleting" or "deleted" or "failed" or "rejected")
            {
                continue;
            }

            var tags = (attachment.Tags ?? []).ToDictionary(tag => tag.Key, tag => tag.Value);

            resources.Add(new Resource(
                $"arn:aws:ec2:{region}:{accountId}:transit-gateway-attachment/{attachment.TransitGatewayAttachmentId}",
                "ec2:transit-gateway-attachment",
                region,
                tags.GetValueOrDefault("Name"),
                new Dictionary<string, string>
                {
                    ["transitGatewayAttachmentId"] = attachment.TransitGatewayAttachmentId,
                    ["transitGatewayId"] = attachment.TransitGatewayId ?? "",
                    ["resourceType"] = attachment.ResourceType?.Value ?? "",
                    ["resourceId"] = attachment.ResourceId ?? "",
                    ["resourceOwnerId"] = attachment.ResourceOwnerId ?? "",
                    ["state"] = attachment.State?.Value ?? "",
                    ["createdAt"] = attachment.CreationTime?.ToString("O") ?? "",
                },
                tags,
                default,
                null,
                null));
        }

        return resources;
    }
}
