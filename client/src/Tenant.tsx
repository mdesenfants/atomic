import * as React from 'react';
import { AtomicCounterClient } from './atomic-counter/build/dist/atomicCounter';
import './GoogleLogin.css';

interface ITenantProps {
    client: AtomicCounterClient | null;
}

interface ITenantState {
    tenantName: string;
    appName: string;
    counterName: string;
    client: AtomicCounterClient | null;
}

export default class Tenant extends React.Component<ITenantProps, ITenantState> {
    constructor(props: ITenantProps) {
        super(props);
        this.state = {
            appName: "",
            client: props.client,
            counterName: "",
            tenantName: "",
        };
    }

    public render() {
        const handle = (evt: React.ChangeEvent<HTMLInputElement>) => this.handleTenantNameChange(evt);
        const handle1 = (evt: React.ChangeEvent<HTMLInputElement>) => this.handleAppNameChange(evt);
        const handle2 = (evt: React.ChangeEvent<HTMLInputElement>) => this.handleCounterNameChange(evt);

        const tenant = () => this.createTenant();
        const counter = () => this.createCounter();

        return [
            (<div>
                <input type="text" value={this.state.tenantName} onChange={handle} />
                <br />
                <button onClick={tenant}>Create Tenant</button>
            </div>),
            (<div>
                <input type="text" value={this.state.appName} onChange={handle1} />
                <input type="text" value={this.state.counterName} onChange={handle2} />
                <br />
                <button onClick={counter}>Create Tenant</button>
            </div>)
        ];
    }

    private async createTenant() {
        if (this.state.client) {
            await this.state.client.createTenant(this.state.tenantName);
        }
    }

    private async createCounter() {
        if (this.state.client) {
            await this.state.client.createCounter(this.state.tenantName, this.state.appName, this.state.counterName);
        }
    }

    private handleTenantNameChange(event: React.ChangeEvent<HTMLInputElement>) {
        this.setState({ tenantName: event.target.value })
    }

    private handleAppNameChange(event: React.ChangeEvent<HTMLInputElement>) {
        this.setState({ appName: event.target.value })
    }

    private handleCounterNameChange(event: React.ChangeEvent<HTMLInputElement>) {
        this.setState({ counterName: event.target.value })
    }
}