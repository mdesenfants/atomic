import * as React from 'react';
import { AtomicCounterClient } from './atomic-counter/build/dist/atomicCounter';
import './GoogleLogin.css';

interface ICounterProps {
    client: AtomicCounterClient | null;
}

interface ICounterState {
    counterName: string;
    client: AtomicCounterClient | null;
}

export class Counter extends React.Component<ICounterProps, ICounterState> {
    constructor(props: ICounterProps) {
        super(props);
        this.state = {
            client: props.client,
            counterName: "",
        };
    }

    public render() {
        const handle = (evt: React.ChangeEvent<HTMLInputElement>) => this.handleCounterNameChange(evt);
        const counter = () => this.createCounter();

        return (
            <div>
                <input type="text" value={this.state.counterName} onChange={handle} />
                <br />
                <button onClick={counter}>Create Counter</button>
            </div>
        );
    }

    private async createCounter() {
        if (this.state.client) {
            await this.state.client.createCounter(this.state.counterName);
        }
    }

    private handleCounterNameChange(event: React.ChangeEvent<HTMLInputElement>) {
        this.setState({ counterName: event.target.value })
    }
}