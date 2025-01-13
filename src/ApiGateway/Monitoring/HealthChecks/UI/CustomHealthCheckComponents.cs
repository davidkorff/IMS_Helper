public class CustomHealthCheckComponents : IHealthCheckUIComponentsBuilder
{
    public Dictionary<string, UIComponent> Build()
    {
        return new Dictionary<string, UIComponent>
        {
            {
                "system-resources",
                new UIComponent
                {
                    Name = "System Resources",
                    CustomStyle = "hc-metric-card system-resources",
                    Template = @"
                        <div class='metric-group'>
                            <div class='metric'>
                                <span class='label'>CPU</span>
                                <span class='value'>{{cpu}}%</span>
                                <div class='progress-bar' style='width: {{cpu}}%'></div>
                            </div>
                            <div class='metric'>
                                <span class='label'>Memory</span>
                                <span class='value'>{{memory}}%</span>
                                <div class='progress-bar' style='width: {{memory}}%'></div>
                            </div>
                            <div class='metric'>
                                <span class='label'>Disk</span>
                                <span class='value'>{{disk}}%</span>
                                <div class='progress-bar' style='width: {{disk}}%'></div>
                            </div>
                        </div>"
                }
            },
            {
                "service-status",
                new UIComponent
                {
                    Name = "Service Status",
                    CustomStyle = "hc-metric-card service-status",
                    Template = @"
                        <div class='service-grid'>
                            {{#each services}}
                            <div class='service-item {{status}}'>
                                <span class='name'>{{name}}</span>
                                <span class='status'>{{status}}</span>
                                <span class='latency'>{{latency}}ms</span>
                            </div>
                            {{/each}}
                        </div>"
                }
            },
            {
                "error-summary",
                new UIComponent
                {
                    Name = "Error Summary",
                    CustomStyle = "hc-metric-card error-summary",
                    Template = @"
                        <div class='error-list'>
                            {{#if errors.length}}
                            <ul>
                                {{#each errors}}
                                <li class='error-item'>
                                    <span class='timestamp'>{{timestamp}}</span>
                                    <span class='message'>{{message}}</span>
                                </li>
                                {{/each}}
                            </ul>
                            {{else}}
                            <p class='no-errors'>No errors in the last hour</p>
                            {{/if}}
                        </div>"
                }
            }
        };
    }
} 