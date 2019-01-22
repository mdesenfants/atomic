import * as React from 'react';
import { AtomicCounterClient } from './atomic-counter/build/dist/atomicCounter';
import './GoogleLogin.css';

interface ICounterProps {
    client: AtomicCounterClient | null;
}

interface ICounterState {
    counterName: string;
    client: AtomicCounterClient | null;
    count: 0;
}

export class Counter extends React.Component<ICounterProps, ICounterState> {
    constructor(props: ICounterProps) {
        super(props);
        this.state = {
            client: props.client,
            count: 0,
            counterName: "",
        };
    }

    public render() {
        const handle = (evt: React.ChangeEvent<HTMLInputElement>) => this.handleCounterNameChange(evt);

        const counter = () => this.createCounter();
        const inc = () => this.increment();
        const count = () => this.count()
        const reset = () => this.reset();

        return (
            <div>
                <input type="text" value={this.state.counterName} onChange={handle} />
                <br />
                <button onClick={counter}>Create Counter</button>
                <p className="App-intro">
                    {this.state.count}
                </p>
                {this.state.client ? <button onClick={inc}>Increment</button> : null}
                {this.state.client ? <button onClick={count}>Count</button> : null}
                {this.state.client ? <button onClick={reset}>Reset</button> : null}
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

    private async increment(): Promise<void> {
        if (this.state.client) {
            await this.state.client.increment(this.state.counterName);
        }
    }

    private async count(): Promise<void> {
        if (this.state.client) {
            const result = await this.state.client.count(this.state.counterName);
            this.setState({ count: result } as any);
        }
    }

    private async reset(): Promise<void> {
        if (this.state.client) {
            await this.state.client.reset(this.state.counterName);
        }
    }
}