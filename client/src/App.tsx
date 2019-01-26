import * as hello from 'hellojs';
import * as React from 'react';
import './App.css';

import Button from 'react-bootstrap/Button';
import DropdownButton from 'react-bootstrap/DropdownButton';
import MenuItem from 'react-bootstrap/DropdownItem'

import { AtomicCounterClient, getAuthToken } from './atomic-counter/build/dist/atomicCounter';

import GoogleLogin from './GoogleLogin';

hello.init({
    google: '1076081007580-rabmg87rit0dcdcc6m29pecc35i0lj5p.apps.googleusercontent.com'
});

interface IAppState {
    client: AtomicCounterClient | null;
    count: number;
    timeoutHandle: NodeJS.Timeout | null;
    counterName: string;
    otherCounters: string[];
}

class App extends React.Component<{}, IAppState> {
    constructor(props: {}) {
        super(props);
        this.state = {
            client: null,
            count: 0,
            counterName: "",
            otherCounters: [],
            timeoutHandle: null,
        };
    }

    public render() {
        const callback = (value: any) => {
            const curr = new AtomicCounterClient(() => getAuthToken(value, 'google'));
            curr.getCounters().then(x => {
                this.setState({ client: curr, otherCounters: x })
            });
        };

        const selectCounter = (input: string) => {
            return () => {
                if (this.state.client) {
                    this.state.client.count(input).then(c => {
                        this.setState({
                            count: c,
                            counterName: input || this.state.counterName,
                        });
                    });
                }
            };
        };

        const handle = (evt: React.ChangeEvent<HTMLInputElement>) => this.handleCounterNameChange(evt);
        const counter = () => this.createCounter();

        const counterToLi = (input: string) =>
            <MenuItem key={input} onClick={selectCounter(input)} active={this.state.counterName === input}>
                {input}
            </MenuItem>;

        return <div className="container">
            <div className="jumbotron">
                <h1>Atomic Counter</h1>
            </div>
            <div className="row">
                <div className="col">
                    {this.state.client ?
                        <div className="form-group">
                            <div className="input-group">
                                <input
                                    type="text"
                                    className="form-control"
                                    value={this.state.counterName}
                                    onChange={handle}
                                    required={true}
                                    pattern="[0-9a-z]+"
                                    placeholder="Counter"
                                    maxLength={58}
                                    minLength={3}
                                />
                                <div className="input-group-append" hidden={this.state.otherCounters.indexOf(this.state.counterName) !== -1}>
                                    <Button className="btn btn-success" onClick={counter}>Create Counter</Button>
                                </div>
                                <DropdownButton className="input-group-append" title="Select a counter" id="existing">
                                    {this.state.otherCounters.map(counterToLi)}
                                </DropdownButton>
                            </div>
                        </div> : null}
                </div>
            </div>
            <div className="row">
                <div className="col">
                    {this.state.client ? null : <GoogleLogin tokenCallback={callback} />}

                </div>
            </div>
            <div className="row">
                <div className="col">
                    {this.state.client ? this.renderTools() : null}
                </div>
            </div>
        </div>;
    }

    private renderTools(): React.ReactNode {
        const inc = () => this.increment();
        const count = () => this.count()
        const reset = () => this.reset();
        const lpad = (input: number) => '0'.repeat(7 - input.toString().length) + input;

        return <div>
            <p>
                Count: {lpad(this.state.count)}
            </p>
            {this.state.client ? <Button onClick={inc}>Increment</Button> : null}
            {this.state.client ? <Button onClick={count}>Count</Button> : null}
            {this.state.client ? <Button onClick={reset}>Reset</Button> : null}
        </div>;
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

export default App;
