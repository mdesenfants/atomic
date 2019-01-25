import * as React from 'react';
import { AtomicCounterClient } from './atomic-counter/build/dist/atomicCounter';
import './Counter.css';

interface ICounterProps {
    client: AtomicCounterClient;
}

interface ICounterState {
    counterName: string;
    client: AtomicCounterClient;
    count: number;
    otherCounters: string[];
}

export class Counter extends React.Component<ICounterProps, ICounterState> {
    constructor(props: ICounterProps) {
        super(props);
        this.state = {
            client: props.client,
            count: 0,
            counterName: "",
            otherCounters: []
        };
    }

    public componentDidMount() {
        this.state.client.getCounters().then(c => this.setState({ otherCounters: c }));
    }

    public render() {
        const handle = (evt: React.ChangeEvent<HTMLInputElement>) => this.handleCounterNameChange(evt);

        const counter = () => this.createCounter();
        const inc = () => this.increment();
        const count = () => this.count()
        const reset = () => this.reset();

        const lpad = (input: number) => {
            let initial = input.toString();
            while (initial.length < 7) {
                initial = '0' + initial;
            }

            return initial;
        };

        const selectCounter = (input: string) => {
            return () => {
                this.state.client.count(input).then(c => {
                    this.setState({
                        count: c,
                        counterName: input || this.state.counterName,
                    });
                });
            };
        };

        const counterToLi = (input: string) => (<li key={input} className="Counter-counter" onClick={selectCounter(input)}>{input}</li>);

        return (
            <div>
                <input type="text" value={this.state.counterName} onChange={handle} required={true} pattern="[0-9a-z]+" placeholder="Counter" maxLength={58} minLength={3} />
                <br />
                <button onClick={counter}>Create Counter</button>
                <p>
                    Count: {lpad(this.state.count)}
                </p>
                {this.state.client ? <button onClick={inc}>Increment</button> : null}
                {this.state.client ? <button onClick={count}>Count</button> : null}
                {this.state.client ? <button onClick={reset}>Reset</button> : null}

                {this.state.otherCounters.length > 0 ? <h2>Other Counters</h2> : null}

                <ul>
                    {this.state.otherCounters.map(counterToLi)}
                </ul>
            </div>
        );
    }

    private async createCounter() {
        if (this.state.client) {
            await this.state.client.createCounter(this.state.counterName);
            this.setState({ otherCounters: await this.state.client.getCounters() })
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